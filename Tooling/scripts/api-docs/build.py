"""Build API HTML with the installed Doxygen's supported header template."""
import argparse
import re
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
DOXY = ROOT / 'docs/doxy'


def build(version, output):
    output = Path(output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    if (output / 'html').exists():
        shutil.rmtree(output / 'html')
    config = (DOXY / 'Gondwana_doxy').read_text()
    config = config.replace('@PROJECT_VERSION@', version)
    config += f'\nOUTPUT_DIRECTORY = "{output}"\nHTML_OUTPUT = html\nGENERATE_LATEX = NO\n'
    generated = output / 'Doxyfile'
    generated.write_text(config)
    header = output / 'header.html'
    subprocess.run(['doxygen', '-w', 'html', str(header), str(output / 'footer.html'),
                    str(output / 'style.css'), str(generated)], cwd=DOXY, check=True)
    html = header.read_text()
    html = html.replace('</head>', '<script defer src="$relpath^api-versions.js"></script>\n</head>')
    # Static label remains visible even if scripts or the manifest cannot load.
    html = html.replace('<div id="titlearea">', '<div id="titlearea">\n'
        '<nav id="api-versions" aria-label="API documentation version">'
        '<strong>$projectnumber</strong> '
        '<span id="api-version-controls">Version list loading…</span></nav>')
    if 'id="api-versions"' not in html:
        raise RuntimeError('Doxygen header no longer contains titlearea')
    header.write_text(html)
    with generated.open('a') as f:
        f.write(f'HTML_HEADER = "{header}"\n')
    subprocess.run(['doxygen', str(generated)], cwd=DOXY, check=True)
    if not (output / 'html/index.html').is_file():
        raise RuntimeError('Doxygen produced no API home page')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--version', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    if not re.fullmatch(r'Version (0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)|Development \(master\) - [A-Za-z0-9.+-]+', args.version):
        parser.error('Expected Version X.Y.Z or Development (master) - NBGV-version')
    build(args.version, args.output)
