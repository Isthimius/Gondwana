"""Apply a scoped API update to a disposable gh-pages checkout (no Git writes)."""
import argparse
import json
import re
import shutil
from pathlib import Path

RELEASE = re.compile(r'v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)\Z')


def redirect(target):
    if target != 'api/' and not RELEASE.fullmatch(target.rstrip('/')):
        raise ValueError('Invalid redirect target')
    return ('<!doctype html><html lang="en"><head><meta charset="utf-8">'
            '<title>Gondwana API</title>'
            f'<meta http-equiv="refresh" content="0;url={target}">'
            f'<link rel="canonical" href="{target}">'
            f'<script>window.location.replace({json.dumps(target)});</script>'
            f'</head><body><p><a href="{target}">Gondwana API</a></p></body></html>\n')


def publish(site, source, version):
    site, source = Path(site), Path(source)
    if version != 'latest' and not RELEASE.fullmatch(version):
        raise ValueError('Only latest or a canonical vX.Y.Z release is publishable')
    if not (source / 'index.html').is_file():
        raise ValueError('Missing generated index.html')
    api = site / 'api'
    api.mkdir(exist_ok=True)
    # Refuse aliases out of the API tree, including manually created symlinks.
    if api.is_symlink() or any(p.is_symlink() for p in api.rglob('*')):
        raise ValueError('API publishing does not support symlinks')
    target = api / version
    if version == 'latest':
        if target.exists():
            shutil.rmtree(target)
        shutil.copytree(source, target)
    elif not target.exists():
        shutil.copytree(source, target)
    elif not (target / 'index.html').is_file():
        raise ValueError(f'Incomplete existing release {version}; repair manually')
    # Existing releases are deliberately never rewritten, even on reruns.
    versions = sorted((p.name for p in api.iterdir() if p.is_dir()
                       and RELEASE.fullmatch(p.name) and (p / 'index.html').is_file()),
                      key=lambda v: tuple(map(int, RELEASE.fullmatch(v).groups())), reverse=True)
    manifest = {'stable': versions[0] if versions else None,
                'development': (api / 'latest/index.html').is_file(), 'releases': versions}
    (api / 'versions.json').write_text(json.dumps(manifest, indent=2) + '\n')
    # Only release publishing owns the stable entry point. Never downgrade it
    # when an older release is retried/backfilled after a newer release.
    if version != 'latest':
        stable = versions[0]
        (api / 'index.html').write_text(redirect(stable + '/'), encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--site', required=True)
    parser.add_argument('--source', required=True)
    parser.add_argument('--version', required=True)
    args = parser.parse_args()
    publish(args.site, args.source, args.version)
