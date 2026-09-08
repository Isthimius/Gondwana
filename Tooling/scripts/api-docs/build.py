"""Build API HTML with the installed Doxygen's supported header template."""
import argparse
import os
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
DOXY = ROOT / 'docs/doxy'


def source_encodings(source_root):
    # Older Windows-authored C# comments contain CP1252 punctuation. Let
    # Doxygen decode those files, without rewriting the exact tag snapshot.
    # UTF-8 remains the default for every file that can be decoded as UTF-8.
    overrides = []
    for directory, _, names in os.walk(source_root):
        for name in names:
            if not name.endswith('.cs'):
                continue
            path = Path(directory) / name
            data = path.read_bytes()
            try:
                data.decode('utf-8')
            except UnicodeDecodeError:
                data.decode('cp1252')  # Fail if this supported fallback is invalid too.
                print('Doxygen input encoding Windows-1252: ' + str(path), flush=True)
                overrides.append('"' + path.as_posix() + '=WINDOWS-1252"')
    return '\nINPUT_FILE_ENCODING += ' + ' '.join(overrides) + '\n' if overrides else ''


def build(version, output, source_root=ROOT):
    source_root = Path(source_root).resolve()
    output = Path(output).resolve()
    if not source_root.is_dir():
        raise ValueError('Source root does not exist')
    if output == source_root or source_root in output.parents:
        raise ValueError('Generated output must be outside the source tree')
    if output.exists() and any(output.iterdir()):
        raise ValueError('Build output must be empty; use a fresh temporary directory')
    output.mkdir(parents=True, exist_ok=True)
    config = (DOXY / 'Gondwana_doxy').read_text(encoding='utf-8')
    config = config.replace('@PROJECT_VERSION@', version)
    config += f'\nOUTPUT_DIRECTORY = "{output}"\nHTML_OUTPUT = html\nGENERATE_LATEX = NO\n'
    # Resolve only input against the tag. All configuration/presentation stays
    # relative to the current tooling checkout's docs/doxy directory.
    config += f'INPUT = "{source_root.as_posix()}"\nSTRIP_FROM_PATH = "{source_root.as_posix()}"\n'
    config += source_encodings(source_root)
    generated = output / 'Doxyfile'
    generated.write_text(config, encoding='utf-8')
    header = output / 'header.html'
    subprocess.run(['doxygen', '-w', 'html', str(header), str(output / 'footer.html'),
                    str(output / 'style.css'), str(generated)], cwd=DOXY, check=True)
    html = header.read_text(encoding='utf-8')
    html = html.replace('</head>', '<script defer src="$relpath^api-versions.js"></script>\n</head>')
    # Static label remains visible even if scripts or the manifest cannot load.
    html = html.replace('<div id="titlearea">', '<div id="titlearea">\n'
        '<nav id="api-versions" aria-label="API documentation version">'
        '<strong>$projectnumber</strong> '
        '<span id="api-version-controls">Version list loading…</span></nav>')
    if 'id="api-versions"' not in html:
        raise RuntimeError('Doxygen header no longer contains titlearea')
    header.write_text(html, encoding='utf-8')
    with generated.open('a', encoding='utf-8') as f:
        f.write(f'HTML_HEADER = "{header}"\n')
    subprocess.run(['doxygen', str(generated)], cwd=DOXY, check=True)
    if not (output / 'html/index.html').is_file():
        raise RuntimeError('Doxygen produced no API home page')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--version', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--source-root', type=Path, default=ROOT)
    args = parser.parse_args()
    if not re.fullmatch(r'Version (0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)|Development \(master\) - [A-Za-z0-9.+-]+', args.version):
        parser.error('Expected Version X.Y.Z or Development (master) - NBGV-version')
    build(args.version, args.output, args.source_root)
