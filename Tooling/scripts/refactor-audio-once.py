"""One-time deterministic edit for the Gondwana audio backend refactor.

This file is intentionally temporary and is removed after branch validation.
"""
from pathlib import Path

path = Path("docs/doxy/Gondwana_doxy")
text = path.read_text(encoding="utf-8-sig")
old = "Gondwana targets desktop, mobile, and web platforms using SkiaSharp for graphics and NAudio for sound."
new = "Gondwana targets desktop and web platforms using SkiaSharp for graphics and pluggable audio backends for desktop and browser targets."
if text.count(old) != 1:
    raise RuntimeError(f"Expected exactly one Doxygen project-brief match; found {text.count(old)}")
path.write_text(text.replace(old, new), encoding="utf-8")
print("Doxygen project brief updated.")
# Trigger the one-time validated persistence run.
