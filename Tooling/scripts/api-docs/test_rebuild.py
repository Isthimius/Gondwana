import json
from pathlib import Path
import tempfile
import shutil
import unittest
from unittest.mock import patch

import rebuild
from build import build, source_encodings, read_html
from publish import redirect
from source_filter import filtered_source


class RebuildTests(unittest.TestCase):
    def test_every_api_workflow_uses_pinned_installer_before_tests(self):
        # The one-time history rebuild workflow was retired after migration.
        # Keep both permanent publishers mandatory rather than skipping missing files.
        for name in ('docs.yml', 'release.yml'):
            with self.subTest(workflow=name):
                workflow = (rebuild.ROOT / '.github/workflows' / name).read_text(encoding='utf-8')
                installer = 'bash Tooling/scripts/api-docs/install-doxygen.sh'
                self.assertIn(installer, workflow)
                self.assertNotIn('apt-get install -y doxygen', workflow)
                self.assertLess(workflow.index(installer), workflow.index('python3 -m unittest'))

    def test_build_rejects_invalid_generated_html(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / 'source'; source.mkdir()
            output = root / 'output'
            def doxygen(args, **kwargs):
                if '-w' in args:
                    (output / 'header.html').write_text('<head></head><div id="titlearea">')
                else:
                    (output / 'html').mkdir()
                    (output / 'html/index.html').touch()
                    (output / 'html/classBroken.html').write_bytes(b'\x97')
            with patch('build.subprocess.run', side_effect=doxygen):
                with self.assertRaisesRegex(ValueError, 'classBroken.html at byte 0'):
                    build('Version 2.1.0', output, source)

    @unittest.skipUnless(shutil.which('doxygen'), 'Requires installed Doxygen')
    def test_real_doxygen_decodes_mixed_source_encodings(self):
        with tempfile.TemporaryDirectory(prefix='api encoding ') as temp:
            root = Path(temp)
            source = root / 'source'; source.mkdir()
            old = source / 'Old.cs'
            old.write_bytes(b'/// <summary>Legacy dash: \x97 and caf\xe9.</summary>\npublic class Legacy {}\n')
            (source / 'New.cs').write_text(
                '/// <summary>UTF-8: \u03a9 and \u2014.</summary>\npublic class Modern {}\n', encoding='utf-8')
            nested = source / 'MixedCase'; nested.mkdir()
            (nested / 'Old.cs').write_text(
                '/// <summary>Same filename, UTF-8: \u03bb.</summary>\npublic class Other {}\n', encoding='utf-8')
            output = root / 'output'
            build('Version 2.1.0', output, source)
            pages = list((output / 'html').glob('class*.html'))
            html = '\n'.join(read_html(p) for p in pages)
            self.assertIn('caf\u00e9', html)
            self.assertIn('\u03a9', html)
            self.assertIn('\u03bb', html)
            self.assertNotIn('\ufffd', html)
            self.assertIn(b'\x97', old.read_bytes())

    def test_invalid_html_reports_filename_and_offset_without_replacing_bytes(self):
        with tempfile.TemporaryDirectory() as temp:
            page = Path(temp) / 'v2.1.0' / 'classScene.html'
            page.parent.mkdir()
            page.write_bytes(b'dash: \x97')
            with self.assertRaises(ValueError) as caught:
                read_html(page)
            self.assertIn(str(page), str(caught.exception))
            self.assertIn('byte 6', str(caught.exception))
            self.assertEqual(page.read_bytes(), b'dash: \x97')
            page.write_text('dash: \u2014', encoding='utf-8')
            self.assertEqual(read_html(page), 'dash: \u2014')

    def test_legacy_encoding_overrides_only_non_utf8_source(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / 'Old.cs').write_bytes(b'// dash: \x96')
            (root / 'New.cs').write_text('// dash: \u2013', encoding='utf-8')
            config = source_encodings(root)
            self.assertIn('INPUT_FILTER =', config)
            self.assertIn('source_filter.py', config)
            self.assertIn('FILTER_SOURCE_FILES = YES', config)
            self.assertEqual(filtered_source(root / 'Old.cs'), '// dash: \u2013'.encode())
            self.assertEqual(filtered_source(root / 'New.cs'), (root / 'New.cs').read_bytes())
            self.assertEqual((root / 'Old.cs').read_bytes(), b'// dash: \x96')
            (root / 'Old.cs').unlink()
            self.assertEqual(source_encodings(root), '')

    def test_input_filter_does_not_reinterpret_other_files_or_ignore_bad_bytes(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            other = root / 'other.txt'; other.write_bytes(b'\x81')
            self.assertEqual(filtered_source(other), b'\x81')
            invalid = root / 'Invalid.cs'; invalid.write_bytes(b'\x81')
            with self.assertRaises(UnicodeDecodeError):
                filtered_source(invalid)
            with self.assertRaises(UnicodeDecodeError):
                source_encodings(root)

    def test_expected_tags_and_semantic_order(self):
        with patch.object(rebuild, 'git', return_value='sha'):
            result = rebuild.validate_tags([*reversed(rebuild.EXPECTED), 'v2.0.1', 'v2.6.0', 'v2.5.3'])
            self.assertEqual(list(result), list(rebuild.EXPECTED))
            for tags in [rebuild.EXPECTED[:-1], [*rebuild.EXPECTED, 'v2.3.1'], [*rebuild.EXPECTED, 'v2.1.0']]:
                with self.assertRaises(ValueError):
                    rebuild.validate_tags(tags)

    def test_redirects_use_both_automatic_mechanisms(self):
        for target in ('api/', 'v2.5.2/'):
            html = redirect(target)
            self.assertIn('window.location.replace(' + json.dumps(target) + ')', html)
            self.assertIn('content="0;url=' + target + '"', html)
            self.assertIn('href="' + target + '"', html)
        self.assertNotIn('v2.5.2', redirect('api/'))

    def test_size_and_file_guards(self):
        with tempfile.TemporaryDirectory() as temp:
            site = Path(temp)
            (site / 'one').write_bytes(b'12345')
            (site / 'two').write_bytes(b'12345')
            self.assertEqual(rebuild.measure(site, 10, 6)['bytes'], 10)
            self.assertEqual(rebuild.measure(site, 10, 6)['files'], 2)
            with self.assertRaises(ValueError):
                rebuild.measure(site, 9, 6)
            with self.assertRaises(ValueError):
                rebuild.measure(site, 10, 5)

    def test_backup_creation_uses_absence_lease_and_verifies(self):
        with patch.object(rebuild, 'remote_ref', side_effect=[None, 'old']), patch.object(rebuild, 'git') as git:
            rebuild.ensure_backup('old', 'refs/heads/backup')
            git.assert_called_once_with('push', '--force-with-lease=refs/heads/backup:', 'origin', 'old:refs/heads/backup')
        for sha, succeeds in [('old', True), ('other', False)]:
            with patch.object(rebuild, 'remote_ref', return_value=sha), patch.object(rebuild, 'git') as git:
                if succeeds:
                    rebuild.ensure_backup('old', 'backup')
                else:
                    with self.assertRaises(ValueError):
                        rebuild.ensure_backup('old', 'backup')
                git.assert_not_called()

    def test_atomic_update_checks_backup_and_concurrent_change(self):
        for refs in [('other',), ('old', 'concurrent')]:
            with patch.object(rebuild, 'remote_ref', side_effect=refs), patch.object(rebuild, 'git') as git:
                with self.assertRaises(ValueError):
                    rebuild.atomic_publish('old', 'backup', 'new')
                git.assert_not_called()
        with patch.object(rebuild, 'remote_ref', side_effect=['old', 'old', 'new']), patch.object(rebuild, 'git') as git:
            rebuild.atomic_publish('old', 'backup', 'new')
            git.assert_called_once_with('push', '--force-with-lease=refs/heads/gh-pages:old', 'origin', 'new:refs/heads/gh-pages')

    def test_wrong_confirmation_cannot_even_read_site(self):
        with self.assertRaises(ValueError):
            rebuild.publish_history('missing', 'missing', 'yes')

    def test_unknown_legacy_content_stops(self):
        baseline = {'index.html': 'blob', 'ai/README.md': 'approved', 'api/latest/index.html': '100644 blob previous'}
        for extra in ('unrelated.txt', 'api/v2.6.0/index.html', 'api/latest/personal.txt'):
            with patch.object(rebuild, 'tree', side_effect=[baseline, dict(baseline, **{extra:'new'})]):
                with self.assertRaises(ValueError):
                    rebuild.audit_legacy('current')
        with patch.object(rebuild, 'tree', side_effect=[baseline, dict(baseline, **{'api/latest/index.html':'100644 blob dev'})]):
            rebuild.audit_legacy('current')
        updated = dict(baseline, **{
            'api/latest/' + name: '100644 blob generated' for name in rebuild.DOXYGEN_114_ASSETS})
        with patch.object(rebuild, 'tree', side_effect=[baseline, updated]):
            rebuild.audit_legacy('current')
        for path, entry in [('api/latest/cookie.js', '120000 blob symlink'),
                            ('api/latest/user-notes.html', '100644 blob unrelated')]:
            with patch.object(rebuild, 'tree', side_effect=[baseline, dict(updated, **{path:entry})]):
                with self.assertRaises(ValueError):
                    rebuild.audit_legacy('current')

    def test_build_refuses_output_inside_source_or_nonempty(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            with self.assertRaises(ValueError):
                build('Version 2.1.0', root / 'output', root)
            source = root / 'source'; source.mkdir()
            output = root / 'output'; output.mkdir()
            (output / 'precious').touch()
            with self.assertRaises(ValueError):
                build('Version 2.1.0', output, source)

    def test_historical_build_keeps_current_assets(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / 'historical'; source.mkdir()
            output = root / 'generated'
            def doxygen(args, **kwargs):
                if '-w' in args:
                    (output / 'header.html').write_text('<head></head><div id="titlearea">')
                else:
                    (output / 'html').mkdir()
                    (output / 'html/index.html').touch()
            with patch('build.subprocess.run', side_effect=doxygen):
                build('Version 2.1.0', output, source)
            config = (output / 'Doxyfile').read_text(encoding='utf-8')
            self.assertIn('INPUT = "' + source.as_posix() + '"', config)
            self.assertIn('HTML_EXTRA_STYLESHEET  = api-versions.css', config)
            self.assertIn('HTML_EXTRA_FILES       = api-versions.js', config)
