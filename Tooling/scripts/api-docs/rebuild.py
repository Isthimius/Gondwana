"""One-time archive rebuild. Prepare/build are safe; publish requires confirmation."""
import argparse
import datetime
import hashlib
import json
import os
import re
from pathlib import Path
import subprocess
import sys
import tarfile
import tempfile

from build import ROOT
from publish import publish, redirect, RELEASE

EXPECTED = tuple('v' + v for v in (
    '2.1.0', '2.1.1', '2.1.2', '2.2.0', '2.2.1', '2.2.2', '2.2.3',
    '2.2.4', '2.3.0', '2.4.0', '2.4.1', '2.4.2', '2.4.3', '2.5.0',
    '2.5.1', '2.5.2'))
# Reviewed generated output and explicitly approved ai/, doxy/, logo/ copies.
REVIEWED_LEGACY = 'b67c960918d46858a1c5f166350322060dede084'
PAGES = 'refs/heads/gh-pages'
MAX_SITE = 750 * 1024 * 1024
MAX_FILE = 100 * 1024 * 1024


def git(*args, cwd=ROOT):
    return subprocess.check_output(
        ['git', '-c', 'safe.directory=' + Path(cwd).resolve().as_posix(), *args],
        cwd=cwd, encoding='utf-8').strip()


def remote_ref(ref):
    lines = git('ls-remote', '--refs', 'origin', ref).splitlines()
    return lines[0].split()[0] if lines else None


def ensure_backup(original, ref):
    existing = remote_ref(ref)
    if existing and existing != original:
        raise ValueError('Backup exists with different content; never overwrite it')
    if not existing:
        git('push', '--force-with-lease=' + ref + ':', 'origin', original + ':' + ref)
    if remote_ref(ref) != original:
        raise ValueError('Backup verification failed')


def validate_tags(tags):
    selected = sorted((t for t in tags if RELEASE.fullmatch(t)
                       and (2, 1, 0) <= tuple(map(int, t[1:].split('.'))) <= (2, 5, 2)),
                      key=lambda t: tuple(map(int, t[1:].split('.'))))
    if selected != list(EXPECTED):
        raise ValueError('Historical tags differ from the expected 16: ' + repr(selected))
    return {tag: git('rev-parse', '--verify', 'refs/tags/' + tag + '^{commit}')
            for tag in selected}


def tree(commit):
    return dict(line.split('\t', 1)[::-1] for line in
                git('ls-tree', '-r', commit).splitlines())


def audit_legacy(commit):
    reviewed = tree(REVIEWED_LEGACY)
    baseline = {p: v for p, v in reviewed.items() if not p.startswith('api/')}
    actual = tree(commit)
    legacy = {p: v for p, v in actual.items() if not p.startswith('api/')}
    changes = sorted(p for p in baseline.keys() | legacy.keys() if baseline.get(p) != legacy.get(p))
    # A normal development update is allowed before preparation, but no new
    # release or unrelated files may be silently thrown away.
    changes += [p for p in actual if p.startswith('api/') and
                not (p.startswith('api/latest/') or p == 'api/versions.json')]
    for path, entry in actual.items():
        if not path.startswith('api/latest/'):
            continue
        relative = path[len('api/latest/'):]
        generated_name = re.fullmatch(
            r'(?:(?:class|struct|interface|namespace|dir_)[A-Za-z0-9_-]+|[A-Za-z0-9_]+_8cs_source)\.(?:html|js|png|svg)', relative)
        search_asset = re.fullmatch(r'search/[a-z0-9_]+\.(?:js|html|css|png|svg)', relative)
        if not entry.startswith('100644 blob ') or not (path in reviewed or generated_name or search_asset):
            changes.append(path)
    if changes:
        raise ValueError('Unreviewed Pages content; stop and review: ' + repr(changes))


def write_json(path, value):
    Path(path).write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def prepare(state, backup_ref=None, require_master=False):
    state = Path(state)
    if state.exists():
        raise ValueError('State file already exists; do not overwrite migration evidence')
    original = remote_ref(PAGES)
    if not original:
        raise ValueError('gh-pages must exist')
    git('fetch', 'origin', PAGES, 'refs/heads/master', '--tags')
    audit_legacy(original)
    tags = validate_tags(git('tag', '-l').splitlines())
    master = remote_ref('refs/heads/master')
    git('cat-file', '-e', master + '^{commit}')
    if require_master and git('rev-parse', 'HEAD') != master:
        raise ValueError('master advanced after checkout; restart with current master tooling')
    backup = backup_ref or ('refs/heads/gh-pages-pre-versioning-' + datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%d'))
    if not re.fullmatch(r'refs/heads/gh-pages-pre-versioning-[0-9]{8}(?:-[A-Za-z0-9-]+)?', backup):
        raise ValueError('Invalid backup ref; use refs/heads/gh-pages-pre-versioning-YYYYMMDD[-suffix]')
    record = dict(original=original, backup=backup, master=master, tags=tags)
    write_json(state, record)  # Record exact SHA before the first remote write.
    ensure_backup(original, backup)
    print(json.dumps(record, indent=2), flush=True)


def extract_source(commit, directory):
    # Git archive is a read-only exact-commit source snapshot; no tag checkout
    # runs hooks, builds packages, or brings historical presentation into use.
    archive = directory.parent / 'source.tar'
    git('archive', '--format=tar', '--output=' + str(archive), commit)
    with tarfile.open(archive) as tf:
        for member in tf.getmembers():
            destination = (directory / member.name).resolve()
            if directory.resolve() not in destination.parents or not (member.isfile() or member.isdir()):
                raise ValueError('Unsafe archive entry: ' + member.name)
        tf.extractall(directory)
    archive.unlink()


def measure(site, max_site=MAX_SITE, max_file=MAX_FILE):
    total = count = 0
    digest = hashlib.sha256()
    def files(directory):
        # DirEntry caches type information on Windows; avoid several stat
        # system calls per generated file in an archive with thousands of files.
        with os.scandir(directory) as entries:
            for entry in sorted(entries, key=lambda e: e.name):
                if entry.is_symlink():
                    raise ValueError('Symlinks are not allowed in the staged site')
                if entry.is_dir(follow_symlinks=False):
                    yield from files(entry.path)
                elif entry.is_file(follow_symlinks=False):
                    yield Path(entry.path), entry.stat().st_size
    for path, size in files(site):
        if size >= max_file:
            raise ValueError('Generated file reaches GitHub 100 MiB limit: ' + str(path))
        count += 1
        total += size
        digest.update(path.relative_to(site).as_posix().encode())
        digest.update(b'\0')
        digest.update(hashlib.sha256(path.read_bytes()).digest())
    if total > max_site:
        raise ValueError(f'Staged site exceeds 750 MiB: {total} bytes in {count} files')
    return dict(bytes=total, files=count, sha256=digest.hexdigest())


def validate_site(site, record):
    site = Path(site)
    if {p.name for p in site.iterdir()} != {'.nojekyll', 'index.html', 'api'}:
        raise ValueError('Unexpected root entries')
    if not (site / '.nojekyll').is_file():
        raise ValueError('Missing .nojekyll file')
    api = site / 'api'
    if {p.name for p in api.iterdir()} != set(EXPECTED) | {'latest', 'index.html', 'versions.json'}:
        raise ValueError('Unexpected or missing API entries')
    expected_manifest = dict(stable='v2.5.2', development=True, releases=list(reversed(EXPECTED)))
    if json.loads((api / 'versions.json').read_text()) != expected_manifest:
        raise ValueError('Invalid version manifest')
    for path, target in [(site / 'index.html', 'api/'), (api / 'index.html', 'v2.5.2/')]:
        if path.read_text(encoding='utf-8') != redirect(target):
            raise ValueError('Invalid automatic redirect: ' + str(path))
    for version in (*EXPECTED, 'latest'):
        folder = api / version
        for asset in ('index.html', 'api-versions.js', 'api-versions.css'):
            if not (folder / asset).is_file():
                raise ValueError(f'Missing {version}/{asset}')
        for asset in ('api-versions.js', 'api-versions.css'):
            if (folder / asset).read_bytes() != (ROOT / 'docs/doxy' / asset).read_bytes():
                raise ValueError('Historical presentation asset differs from current master')
        pages = list(folder.glob('class*.html'))
        if not pages:
            raise ValueError('No representative API class pages: ' + version)
        provenance = json.loads((folder / 'source.json').read_text())
        expected_sha = record['master'] if version == 'latest' else record['tags'][version]
        if provenance != dict(version=version, commit=expected_sha):
            raise ValueError('Source provenance mismatch')
        for page in folder.rglob('*.html'):
            html = page.read_text(encoding='utf-8')
            if 'v2.5.3' in html or 'Version 2.5.3' in html:
                raise ValueError('Fictional release label in ' + str(page))
        for page in [folder / 'index.html', *pages]:
            html = page.read_text(encoding='utf-8')
            if 'id="api-versions"' not in html or 'api-versions.js' not in html or 'api-versions.css' not in html:
                raise ValueError('Missing selector on ' + str(page))
        home = (folder / 'index.html').read_text(encoding='utf-8')
        label = 'Development (master)' if version == 'latest' else 'Version ' + version[1:]
        if label not in home or (version == 'latest' and 'unreleased' not in home):
            raise ValueError('Incorrect release/development label')
    return measure(site)


def build_history(state, destination):
    record = json.loads(Path(state).read_text())
    if remote_ref(record['backup']) != record['original']:
        raise ValueError('Verified remote backup is required before generation')
    destination = Path(destination).resolve()
    if destination == ROOT or ROOT in destination.parents:
        raise ValueError('Staging output must be outside the tooling checkout')
    destination.mkdir()  # Never reuse or erase an existing staging directory.
    site = destination / 'site'
    site.mkdir()
    (site / '.nojekyll').touch()
    (site / 'index.html').write_text(redirect('api/'), encoding='utf-8')
    for version, commit in [*record['tags'].items(), ('latest', record['master'])]:
        print('Building ' + version + ' from ' + commit, flush=True)
        with tempfile.TemporaryDirectory(prefix='gondwana-api-') as temporary:
            temporary = Path(temporary)
            source = temporary / 'source'
            extract_source(commit, source)
            label = ('Development (master) - ' + json.loads((source / 'version.json').read_text())['version'] + '-unreleased'
                     if version == 'latest' else 'Version ' + version[1:])
            output = temporary / 'build'
            with (destination / (version + '.log')).open('w', encoding='utf-8') as log:
                subprocess.run([sys.executable, str(ROOT / 'Tooling/scripts/api-docs/build.py'),
                                '--version', label, '--source-root', str(source), '--output', str(output)],
                               stdout=log, stderr=subprocess.STDOUT, check=True)
            write_json(output / 'html/source.json', dict(version=version, commit=commit))
            publish(site, output / 'html', version)
    result = validate_site(site, record)
    write_json(destination / 'report.json', dict(**record, **result))
    print(json.dumps(result), flush=True)


def atomic_publish(original, backup, commit):
    if remote_ref(backup) != original:
        raise ValueError('Backup changed or disappeared')
    if remote_ref(PAGES) != original:
        raise ValueError('gh-pages changed; abort instead of overwriting concurrent work')
    git('push', '--force-with-lease=' + PAGES + ':' + original, 'origin', commit + ':' + PAGES)
    if remote_ref(PAGES) != commit:
        raise ValueError('Publication verification failed; inspect remote state before retrying')


def publish_history(state, destination, confirmation):
    if confirmation != 'REBUILD_GH_PAGES':
        raise ValueError('Exact REBUILD_GH_PAGES confirmation is required')
    record = json.loads(Path(state).read_text())
    destination = Path(destination).resolve()
    site = destination / 'site'
    result = validate_site(site, record)
    report = json.loads((destination / 'report.json').read_text())
    if report != dict(**record, **result):
        raise ValueError('Staged tree changed since validation')
    # Use a temporary index to create ONE parentless site commit. No duplicate
    # intermediate artifacts or old generated blobs enter the new history.
    with tempfile.TemporaryDirectory(prefix='gondwana-index-') as temp:
        env = dict(os.environ, GIT_INDEX_FILE=str(Path(temp) / 'index'),
                   GIT_WORK_TREE=str(site), GIT_DIR=git('rev-parse', '--absolute-git-dir'))
        subprocess.run(['git', 'add', '--all', '--', '.'], cwd=site, env=env, check=True)
        tree_sha = subprocess.check_output(['git', 'write-tree'], cwd=site, env=env, text=True).strip()
    commit = git('commit-tree', tree_sha, '-m', 'Rebuild API archive; backup: ' + record['backup'])
    atomic_publish(record['original'], record['backup'], commit)
    write_json(destination / 'published.json', dict(commit=commit, **record, **result))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['prepare', 'build', 'validate', 'publish'])
    parser.add_argument('--state', required=True)
    parser.add_argument('--destination')
    parser.add_argument('--confirmation', default='')
    parser.add_argument('--backup-ref')
    parser.add_argument('--require-master', action='store_true')
    args = parser.parse_args()
    if args.action == 'prepare':
        prepare(args.state, args.backup_ref, args.require_master)
    elif not args.destination:
        parser.error('--destination is required')
    elif args.action == 'build':
        build_history(args.state, args.destination)
    elif args.action == 'validate':
        print(validate_site(Path(args.destination) / 'site', json.loads(Path(args.state).read_text())))
    else:
        publish_history(args.state, args.destination, args.confirmation)
