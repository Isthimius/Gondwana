from pathlib import Path
import shutil
import tempfile
import unittest
from unittest.mock import patch

from build import ROOT, build, source_encodings, read_html
from source_filter import filtered_source


class BuildTests(unittest.TestCase):
    def test_every_api_workflow_uses_pinned_installer_before_tests(self):
        for name in ('docs.yml', 'release.yml'):
            with self.subTest(workflow=name):
                workflow = (ROOT / '.github/workflows' / name).read_text(encoding='utf-8')
                installer = 'bash Tooling/scripts/api-docs/install-doxygen.sh'
                self.assertIn(installer, workflow)
                self.assertNotIn('apt-get install -y doxygen', workflow)
                self.assertIn('python3 -m unittest', workflow)
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

    def test_custom_source_root_keeps_current_assets(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / 'source'; source.mkdir()
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


if __name__ == '__main__':
    unittest.main()
