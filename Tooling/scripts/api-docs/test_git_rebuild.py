"""Exercise real Git refs and leases using a disposable local bare remote."""
from pathlib import Path
import json
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import rebuild


class GitRebuildTests(unittest.TestCase):
    def test_backup_atomic_replacement_and_racing_writer(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            remote = root / 'remote.git'
            repo = root / 'repo'
            subprocess.run(['git', 'init', '--bare', str(remote)], check=True, capture_output=True)
            subprocess.run(['git', 'init', str(repo)], check=True, capture_output=True)
            def git(*args):
                return subprocess.check_output(['git', *args], cwd=repo, text=True, stderr=subprocess.PIPE).strip()
            git('config', 'user.name', 'API test')
            git('config', 'user.email', 'api-test@example.invalid')
            git('remote', 'add', 'origin', str(remote))
            (repo / 'index.html').write_text('legacy')
            git('add', '.')
            git('commit', '-m', 'original')
            original = git('rev-parse', 'HEAD')
            git('push', 'origin', 'HEAD:' + rebuild.PAGES)
            (repo / 'index.html').write_text('replacement')
            git('add', '.')
            git('commit', '-m', 'replacement')
            replacement = git('rev-parse', 'HEAD')
            (repo / 'index.html').write_text('concurrent writer')
            git('add', '.')
            git('commit', '-m', 'concurrent')
            concurrent = git('rev-parse', 'HEAD')
            backup = 'refs/heads/gh-pages-pre-versioning-test'
            with patch.object(rebuild, 'git', side_effect=git):
                rebuild.ensure_backup(original, backup)
                rebuild.ensure_backup(original, backup)
                with self.assertRaises(ValueError):
                    rebuild.ensure_backup(replacement, backup)
                self.assertEqual(rebuild.remote_ref(rebuild.PAGES), original)
                git('push', 'origin', concurrent + ':' + rebuild.PAGES)
                with self.assertRaises(ValueError):
                    rebuild.atomic_publish(original, backup, replacement)
                # Simulate a writer winning AFTER the preflight reads. The
                # server-side lease must still reject the final update.
                with patch.object(rebuild, 'remote_ref', side_effect=[original, original]):
                    with self.assertRaises(subprocess.CalledProcessError):
                        rebuild.atomic_publish(original, backup, replacement)
                self.assertEqual(rebuild.remote_ref(rebuild.PAGES), concurrent)
                second_backup = backup + '-second'
                rebuild.ensure_backup(concurrent, second_backup)
                rebuild.atomic_publish(concurrent, second_backup, replacement)
                self.assertEqual(rebuild.remote_ref(rebuild.PAGES), replacement)
                self.assertEqual(rebuild.remote_ref(backup), original)
                self.assertEqual(rebuild.remote_ref(second_backup), concurrent)
                # The final migration creates one parentless commit containing
                # only the staged site, with no intermediate build artifacts.
                third_backup = backup + '-third'
                rebuild.ensure_backup(replacement, third_backup)
                staging = root / 'staging'; staging.mkdir()
                site = staging / 'site'; site.mkdir()
                (site / '.nojekyll').touch()
                (site / 'index.html').write_text('redirect')
                record = dict(original=replacement, backup=third_backup)
                state = root / 'state.json'
                state.write_text(json.dumps(record))
                measured = rebuild.measure(site)
                (staging / 'report.json').write_text(json.dumps(dict(**record, **measured)))
                with patch.object(rebuild, 'validate_site', return_value=measured):
                    rebuild.publish_history(state, staging, 'REBUILD_GH_PAGES')
                published = rebuild.remote_ref(rebuild.PAGES)
                self.assertEqual(git('rev-list', '--parents', '-n', '1', published), published)
                self.assertEqual(git('ls-tree', '--name-only', published).splitlines(), ['.nojekyll', 'index.html'])
                self.assertEqual(rebuild.remote_ref(third_backup), replacement)
