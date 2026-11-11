#!/usr/bin/env python3
"""Render active PlantUML diagrams as SVG assets for the synchronized Wiki."""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "Solution Items" / "documentation"
DESTINATION = ROOT / "docs" / "wiki" / "diagrams"


def render(jar: Path) -> list[Path]:
    """Render active top-level diagrams; never touch the old/ archive."""
    if not jar.is_file():
        raise FileNotFoundError(f"PlantUML JAR not found: {jar}")
    sources = sorted(SOURCE.glob("*.puml"))
    if not sources:
        raise RuntimeError(f"No active PlantUML diagrams found in {SOURCE}")
    DESTINATION.mkdir(parents=True, exist_ok=True)
    outputs: list[Path] = []
    with tempfile.TemporaryDirectory(prefix="gondwana-plantuml-") as temporary:
        work = Path(temporary)
        for source in sources:
            shutil.copy2(source, work / source.name)
        subprocess.run(
            [
                "java", "-jar", str(jar.resolve()), "-tsvg",
                "-charset", "UTF-8", "-failfast2",
                *[str(work / source.name) for source in sources],
            ],
            check=True,
            cwd=work,
        )
        for source in sources:
            generated = work / f"{source.stem}.svg"
            if not generated.is_file():
                raise RuntimeError(f"PlantUML did not produce {generated.name}")
            payload = generated.read_bytes()
            if not payload or b"<svg" not in payload:
                raise RuntimeError(f"PlantUML produced invalid SVG for {source.name}")
            try:
                root = ET.fromstring(payload)
            except ET.ParseError as error:
                raise RuntimeError(f"Invalid SVG for {source.name}: {error}") from error
            if root.tag != "{http://www.w3.org/2000/svg}svg":
                raise RuntimeError(f"Unexpected SVG root for {source.name}: {root.tag}")
            destination = DESTINATION / generated.name
            if not destination.exists() or destination.read_bytes() != payload:
                destination.write_bytes(payload)
                print(f"Updated {destination.relative_to(ROOT)}")
            outputs.append(destination)
    expected = {path.name for path in outputs}
    for previous in DESTINATION.glob("*.svg"):
        if previous.name not in expected:
            previous.unlink()
            print(f"Removed obsolete {previous.relative_to(ROOT)}")
    print(f"Rendered {len(outputs)} active PlantUML diagrams.")
    return outputs


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--jar", type=Path, required=True, help="Pinned PlantUML JAR")
    options = parser.parse_args()
    try:
        render(options.jar)
    except (OSError, RuntimeError, subprocess.CalledProcessError) as error:
        print(f"PlantUML render failed: {error}", file=sys.stderr)
        sys.exit(1)
