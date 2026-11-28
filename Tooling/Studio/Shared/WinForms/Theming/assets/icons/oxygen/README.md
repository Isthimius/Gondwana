# KDE Oxygen icon artwork

This directory contains selected Oxygen icons for Gondwana Studio and its
standalone editors.

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
markers. The assigned file-operation and editing commands load loose Oxygen artwork through
the shared icon resolver, preferring exact or next-larger native sizes and using
the largest smaller native size when needed. The existing vector glyphs remain
fallbacks when the Oxygen artwork is absent. Other commands retain their
existing artwork until individually migrated.

## Theme and resolution overrides

The resolution directories directly under this folder are the common artwork
for every theme. Optional overrides live under `themes/Dark/`,
`themes/DarkBlue/` or `themes/Light/`, with the same nine resolution
directories and the same logical icon filename.

For example, a Light-only variant of Save at 32 pixels belongs in
`themes/Light/32x32/document-save.png`. Only add overrides that are needed;
there is no requirement to duplicate the common icon set. A different
upstream image may be assigned to the same logical filename; record its
original path, revision, corresponding source and any modifications below.

The shared resolver searches the active theme first: exact requested device
pixels, then the next larger available native size, then the largest smaller
size. Only when that theme has no matching icon at any size does it repeat
the search in the common directories. Thus a single theme-specific 32-pixel
image overrides the common image at every requested size for that theme.
No artwork from another theme is used. The original command glyph is the
final fallback if neither set has the icon. Native files are not resampled
on disk; the resolver returns a resized, independently owned bitmap.

Theme selection uses the current process-wide appearance (restart to apply
a changed preference). The size comes from the requesting control, including
DPI-aware toolbars; this does not change existing navigation or toolbar
layout settings.

## Imported artwork inventory

| Command | Upstream path and revision | Local files | Corresponding source / modifications |
| --- | --- | --- | --- |
| File operations — New | [actions/document-new.png and scalable sources](https://github.com/KDE/oxygen-icons/tree/6d2838930cea3aa757ea811d7c2b838f206ed64c) | `16x16/`, `22x22/`, `32x32/`, `48x48/` — `document-new.png` | `source/scalable/actions/document-new.svgz` and `source/scalable/actions/small/{16x16,22x22,48x48}/document-new.svgz`; unchanged upstream bytes |
| File operations — Open | [actions/document-open.png](https://github.com/KDE/oxygen-icons/blob/6d2838930cea3aa757ea811d7c2b838f206ed64c/32x32/actions/document-open.png) | `16x16/`, `22x22/`, `32x32/`, `48x48/` — `document-open.png` | `source/scalable/actions/small/{16x16,22x22,32x32,48x48}/document-open.svgz`; unchanged upstream bytes |
| File operations — Save | [actions/document-save.png](https://github.com/KDE/oxygen-icons/blob/6d2838930cea3aa757ea811d7c2b838f206ed64c/32x32/actions/document-save.png) | `16x16/`, `22x22/`, `32x32/`, `48x48/` — `document-save.png` | `source/scalable/actions/document-save.svgz` and `source/scalable/actions/small/{16x16,22x22,48x48}/document-save.svgz`; unchanged upstream bytes |
| File operations — Save All | [actions/document-save-all.png](https://github.com/KDE/oxygen-icons/blob/6d2838930cea3aa757ea811d7c2b838f206ed64c/32x32/actions/document-save-all.png) | `16x16/`, `22x22/`, `32x32/`, `48x48/` — `document-save-all.png` | `source/scalable/actions/document-save-all.svgz` and `source/scalable/actions/small/{16x16,22x22,48x48}/document-save-all.svgz`; unchanged upstream bytes |
| File operations — Save As | [actions/document-save-as.png](https://github.com/KDE/oxygen-icons/blob/6d2838930cea3aa757ea811d7c2b838f206ed64c/32x32/actions/document-save-as.png) | `16x16/`, `22x22/`, `32x32/`, `48x48/` — `document-save-as.png` | `source/scalable/actions/document-save-as.svgz` and `source/scalable/actions/small/{16x16,22x22,48x48}/document-save-as.svgz`; unchanged upstream bytes |
| File operations — Directory | [places/folder-activities.png](https://github.com/KDE/oxygen-icons/blob/6d2838930cea3aa757ea811d7c2b838f206ed64c/32x32/places/folder-activities.png) | `8x8/`, `16x16/`, `22x22/`, `32x32/`, `48x48/`, `128x128/`, `256x256/` — `folder-activities.png` | `source/scalable/places/folder-activities.svgz` and `source/scalable/places/small/{16x16,22x22,32x32,48x48,64x64}/folder-activities.svgz`; unchanged upstream bytes |
| Editing — Add | [actions/list-add.png](https://github.com/KDE/oxygen-icons/blob/6d2838930cea3aa757ea811d7c2b838f206ed64c/32x32/actions/list-add.png) | `16x16/`, `22x22/`, `32x32/`, `48x48/`, `128x128/` — `list-add.png` | `source/scalable/actions/` — base; small 16, 22, 32 SVGZ variants for `list-add`; unchanged upstream bytes |
| Editing — Remove | [actions/list-remove.png](https://github.com/KDE/oxygen-icons/blob/6d2838930cea3aa757ea811d7c2b838f206ed64c/32x32/actions/list-remove.png) | `16x16/`, `22x22/`, `32x32/`, `48x48/`, `64x64/`, `128x128/` — `list-remove.png` | `source/scalable/actions/` — small 16, 22, 32 SVGZ variants for `list-remove`; unchanged upstream bytes |
| Editing — Delete | [actions/edit-delete.png](https://github.com/KDE/oxygen-icons/blob/6d2838930cea3aa757ea811d7c2b838f206ed64c/32x32/actions/edit-delete.png) | `16x16/`, `22x22/`, `32x32/`, `48x48/` — `edit-delete.png` | `source/scalable/actions/` — base; small 22 SVGZ variants for `edit-delete`; unchanged upstream bytes |
| Editing — Duplicate | [actions/edit-copy.png](https://github.com/KDE/oxygen-icons/blob/6d2838930cea3aa757ea811d7c2b838f206ed64c/32x32/actions/edit-copy.png) | `16x16/`, `22x22/`, `32x32/`, `48x48/` — `edit-copy.png` | `source/scalable/actions/` — base; small 16, 22, 48 SVGZ variants for `edit-copy`; unchanged upstream bytes |
| Editing — Rename | [actions/document-edit.png](https://github.com/KDE/oxygen-icons/blob/6d2838930cea3aa757ea811d7c2b838f206ed64c/32x32/actions/document-edit.png) | `16x16/`, `22x22/`, `32x32/`, `48x48/`, `64x64/`, `128x128/` — `document-edit.png` | `source/scalable/actions/` — base; small 16, 22, 32, 48 SVGZ variants for `document-edit`; unchanged upstream bytes |
| Editing — Replace | [actions/edit-find-replace.png](https://github.com/KDE/oxygen-icons/blob/6d2838930cea3aa757ea811d7c2b838f206ed64c/32x32/actions/edit-find-replace.png) | `16x16/`, `22x22/`, `32x32/` — `edit-find-replace.png` | `source/scalable/actions/` — small 16, 22, 32 SVGZ variants for `edit-find-replace`; unchanged upstream bytes |
| Editing — Prune | [actions/edit-clear.png](https://github.com/KDE/oxygen-icons/blob/6d2838930cea3aa757ea811d7c2b838f206ed64c/32x32/actions/edit-clear.png) | `16x16/`, `22x22/`, `32x32/`, `48x48/` — `edit-clear.png` | `source/scalable/actions/` — base; small 16, 22 SVGZ variants for `edit-clear`; unchanged upstream bytes |
| Editing — Apply | [actions/dialog-ok-apply.png](https://github.com/KDE/oxygen-icons/blob/6d2838930cea3aa757ea811d7c2b838f206ed64c/32x32/actions/dialog-ok-apply.png) | `16x16/`, `22x22/`, `32x32/`, `48x48/`, `64x64/`, `128x128/`, `256x256/` — `dialog-ok-apply.png` | `source/scalable/actions/` — base; small 16, 22 SVGZ variants for `dialog-ok-apply`; unchanged upstream bytes |
| Editing — Cancel | [actions/dialog-cancel.png](https://github.com/KDE/oxygen-icons/blob/6d2838930cea3aa757ea811d7c2b838f206ed64c/32x32/actions/dialog-cancel.png) | `16x16/`, `22x22/`, `32x32/`, `48x48/`, `64x64/`, `128x128/`, `256x256/` — `dialog-cancel.png` | `source/scalable/actions/` — base; small 16, 22 SVGZ variants for `dialog-cancel`; unchanged upstream bytes |
