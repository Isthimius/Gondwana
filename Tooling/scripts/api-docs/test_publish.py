import tempfile
import unittest
from pathlib import Path
import json
from publish import publish, redirect


def snapshot(path):
    return {str(p.relative_to(path)): p.read_bytes() for p in path.rglob('*') if p.is_file()}


class PublishingTests(unittest.TestCase):
    def test_tracks_immutability_reruns_and_semver(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            site, source = root / 'site', root / 'html'
            site.mkdir(); source.mkdir()
            (site / 'wiki').mkdir()
            (site / 'wiki/Home.html').write_text('preserve wiki')
            (site / 'index.html').write_text('preserve old root')
            (source / 'index.html').write_text('old release')
            (source / 'classScene.html').write_text('class page')
            publish(site, source, 'v2.9.0')
            old = snapshot(site / 'api/v2.9.0')
            (source / 'index.html').write_text('new release')
            publish(site, source, 'v2.10.0')
            stable = (site / 'api/index.html').read_bytes()
            publish(site, source, 'v2.9.0')
            self.assertEqual(old, snapshot(site / 'api/v2.9.0'))
            self.assertEqual(stable, (site / 'api/index.html').read_bytes())
            releases = snapshot(site / 'api')
            (source / 'index.html').write_text('development')
            publish(site, source, 'latest')
            for name, content in releases.items():
                if name != 'versions.json':
                    self.assertEqual(content, (site / 'api' / name).read_bytes())
            once = snapshot(site)
            publish(site, source, 'latest')
            self.assertEqual(once, snapshot(site))
            (source / 'classScene.html').unlink()
            publish(site, source, 'latest')
            self.assertFalse((site / 'api/latest/classScene.html').exists())
            manifest = json.loads((site / 'api/versions.json').read_text())
            self.assertEqual(manifest, {'stable': 'v2.10.0', 'development': True,
                                        'releases': ['v2.10.0', 'v2.9.0']})
            self.assertEqual((site / 'wiki/Home.html').read_text(), 'preserve wiki')
            self.assertEqual((site / 'index.html').read_text(), 'preserve old root')

    def test_first_development_does_not_claim_stable(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'html'; source.mkdir()
            (source / 'index.html').write_text('dev')
            publish(root, source, 'latest')
            self.assertFalse((root / 'api/index.html').exists())
            self.assertIsNone(json.loads((root / 'api/versions.json').read_text())['stable'])

    def test_invalid_versions_and_incomplete_releases_fail(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'html'; source.mkdir()
            (source / 'index.html').write_text('dev')
            for version in ['master', '../wiki', 'v02.0.0', 'v2.0.0-rc.1', 'abcd123']:
                with self.assertRaises(ValueError):
                    publish(root, source, version)
            (root / 'api/v1.0.0').mkdir(parents=True)
            with self.assertRaises(ValueError):
                publish(root, source, 'v1.0.0')

    def test_redirects_use_both_automatic_mechanisms(self):
        for target in ('api/', 'v2.5.2/'):
            html = redirect(target)
            self.assertIn('window.location.replace(' + json.dumps(target) + ')', html)
            self.assertIn('content="0;url=' + target + '"', html)
            self.assertIn('href="' + target + '"', html)
        self.assertNotIn('v2.5.2', redirect('api/'))


if __name__ == '__main__':
    unittest.main()
