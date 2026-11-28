# KDE Oxygen icon artwork

This directory reserves space for selected Oxygen icons for Gondwana Studio
and its standalone editors. No Oxygen PNGs have been imported yet.

Oxygen icons copyright KDE and the Oxygen contributors, licensed under the GNU
Lesser General Public License version 3 or later (LGPL-3.0-or-later).
See [COPYING](COPYING), [AUTHORS](AUTHORS), [LGPL v3](LGPL-3.0-or-later.txt)
and the incorporated [GPL v3](GPL-3.0-only.txt). Gondwana's original code remains MIT.

- Upstream project: https://develop.kde.org/frameworks/oxygen-icons/
- Upstream repository: https://github.com/KDE/oxygen-icons
- Notice and contributor files copied from upstream commit [6d28389](https://github.com/KDE/oxygen-icons/tree/6d2838930cea3aa757ea811d7c2b838f206ed64c).
- GPL v3 text: [SPDX license-list-data](https://github.com/spdx/license-list-data/blob/cc4fe1ac462bd3ad3c81a07234baf7d9541ee871/text/GPL-3.0-only.txt).

## Layout and import workflow

Each resolution has a separate folder: `8x8`, `16x16`, `22x22`, `24x24`, `32x32`, `48x48`, `64x64`, `128x128`, `256x256`.
The `source/` folder is reserved for corresponding editable artwork (SVG,
layered artwork where applicable, otherwise original PNG), following Oxygen's
COPYING clarification.

When importing an icon:

1. Identify the semantic command (for example Save, Paint or Rotate Left) and
   the upstream icon path and revision. Keep original filenames where practical.
2. Put the supplied transparent PNG in its actual native resolution folder.
   Add other native sizes only when supplied or obtained from the same artwork.
   Do not silently upscale a smaller image or assume all resolutions exist.
3. Retain corresponding source and record modifications or derived files.
4. Add the entry below, and reuse the artwork for the same command across
   Studio and the standalones. Different commands may share an icon when
   their meaning is equivalent.
5. Verify contrast on Dark, Dark Blue and Light and check the supported DPI
   sizes before replacing existing button artwork.

The project keeps these assets separate from the legacy embedded icons.
Build and publish copy this directory as loose content, excluding empty-folder
markers. Runtime loading and button migration are intentionally deferred; the
existing icon resolver and artwork are unchanged.

## Imported artwork inventory

| Command | Upstream path and revision | Local files | Corresponding source / modifications |
| --- | --- | --- | --- |
