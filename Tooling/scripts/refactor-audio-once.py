"""One-time deterministic edits for the Gondwana audio backend refactor.

This file is intentionally temporary and is removed after the branch validation run.
"""
from pathlib import Path


def replace_required(path: str, old: str, new: str) -> None:
    file = Path(path)
    text = file.read_text(encoding="utf-8-sig")
    if old not in text:
        raise RuntimeError(f"Expected text not found in {path}: {old!r}")
    if text.count(old) != 1:
        raise RuntimeError(f"Expected exactly one match in {path}: {old!r}")
    text = text.replace(old, new)
    file.write_text(text, encoding="utf-8")


def insert_after_required(path: str, marker: str, addition: str) -> None:
    file = Path(path)
    text = file.read_text(encoding="utf-8-sig")
    if marker not in text:
        raise RuntimeError(f"Expected marker not found in {path}: {marker!r}")
    if text.count(marker) != 1:
        raise RuntimeError(f"Expected exactly one marker in {path}: {marker!r}")
    text = text.replace(marker, marker + addition)
    file.write_text(text, encoding="utf-8")


replace_required(
    "docs/doxy/Gondwana_doxy",
    "Gondwana targets desktop, mobile, and web platforms using SkiaSharp for graphics and NAudio for sound.",
    "Gondwana targets desktop and web platforms using SkiaSharp for graphics and pluggable audio backends for desktop and browser targets.",
)

insert_after_required(
    "README.md",
    "| [`Gondwana.Input.SDL2`](https://www.nuget.org/packages/Gondwana.Input.SDL2) | Cross-platform SDL2 gamepad input; requires native SDL2 |\n",
    "| [`Gondwana.Audio.NAudio`](https://www.nuget.org/packages/Gondwana.Audio.NAudio) | Windows desktop audio backend using NAudio |\n",
)

replace_required(
    "Gondwana/README.md",
    "-   NAudio-based audio playback",
    "-   Backend-neutral audio resource and playback contracts",
)
insert_after_required(
    "Gondwana/README.md",
    "-   `Gondwana.Audio.Browser` --- Browser-based audio playback support\n",
    "-   `Gondwana.Audio.NAudio` --- Windows desktop audio playback through NAudio\n",
)

replace_required(
    "Tooling/Gondwana.Cli/README.md",
    "| `audio` | `Gondwana.Audio.Browser` for Blazor; desktop audio is already in `Gondwana` core |",
    "| `audio` | `Gondwana.Audio.Browser` for Blazor; `Gondwana.Audio.NAudio` for desktop projects |",
)

replace_required(
    "docs/wiki/Gondwana-CLI-Cheatsheet.md",
    "| `audio` | `Gondwana.Audio.Browser` for Blazor; desktop audio is already included in `Gondwana` core |",
    "| `audio` | `Gondwana.Audio.Browser` for Blazor; `Gondwana.Audio.NAudio` for desktop projects |",
)

insert_after_required(
    "docs/ai/repository-map.md",
    "- `Gondwana.Audio.Browser/` — browser audio integration.\n",
    "- `Gondwana.Audio.NAudio/` — Windows desktop audio backend using NAudio.\n",
)

insert_after_required(
    "Tooling/Gondwana.Templates/README.md",
    "-   `Gondwana.Audio.Browser` --- Browser-based audio playback support\n",
    "-   `Gondwana.Audio.NAudio` --- Windows desktop audio playback through NAudio\n",
)

replace_required(
    "Demos/Slider/Puzzle.cs",
    "            //Engine.Instance.InitializeWinFormsAudioFormats();",
    "            Engine.Instance.InitializeWinFormsAudioFormats();",
)

print("Audio refactor repository edits applied successfully.")
