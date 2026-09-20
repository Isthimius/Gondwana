# Gondwana Scenes (WinForms)

Standalone .NET 8 Windows editor for Gondwana `.gscn` scene definitions.

Run from the repository root:

```console
dotnet run --project Tooling/Gondwana.Tooling.Scenes.WinForms -c Release
```

## Design

The editor is definition-driven. It edits `SceneDefinition` directly and does
not boot a `GameHost`, register an editor scene in the global runtime scene
collection, or require rendering/input hosts simply to author GSCN.

`SceneEditorControl` is a public, hostable WinForms `UserControl`. The
standalone application hosts the same control that Gondwana Studio can embed
later.

The preview uses Gondwana's existing `SceneLayer` coordinate conversion math
through a tiny editor-owned projection layer. It therefore follows the engine's
orthogonal, isometric, hexagonal, and oblique grid transforms without turning
the editor document into a live runtime `Scene`.

## Standalone workflow

The application follows the same outer-document pattern as the other Gondwana
WinForms tools:

- **Working directory** is an outer docked tool window.
- Each open `.gscn` is one outer docked document.
- Double-clicking a GSCN opens it.
- Double-clicking a GTS or GANI adds it as an authoring source to the active
  scene document.
- **Ctrl+S**, **Ctrl+Shift+S**, and **Ctrl+W** save, Save As, and close.
- Unsaved documents prompt before closing.

The working-directory browser shows `.gscn`, `.gts`, and `.gani` files.

## Editor-local docking

Each `SceneEditorControl` owns a nested DockPanelSuite workspace with:

- **Scene structure** — scene/layer navigation and layer add/remove commands.
- **Scene preview** — definition-driven composition and tile selection.
- **GTS frame sources** — GTS region/frame browser with thumbnails and frame
  assignment.
- **GANI animations** — animation-key sources and assignment.
- **Properties** — selected scene or layer properties.
- **Tile properties** — selected grid cell state.
- **Validation** — structural errors and authoring-source diagnostics.

These panes can be split or tabbed only inside their owning scene editor.
Floating and auto-hide are disabled by the shared `EditorDockWorkspace`.

Closing a pane hides it rather than disposing it. Use **View** to restore any
individual pane in the active scene document, or choose **Show all scene panes**.
The same menu restores the outer **Working directory** window. Reopened inner
panes return to their previous split/tab group for the current document.

Layout persistence is intentionally not implemented; reopening a document uses
the default layout.

## Sparse tile editing

GSCN supports sparse tile definitions. Merely selecting a grid cell does not add
a `SceneLayerTileDefinition`.

The editor creates a tile definition only when the user changes a tile property,
assigns a GTS frame, or assigns a GANI animation. **Clear tile** removes that
explicit entry and returns the cell to GSCN defaults.

This keeps large mostly-empty layers compact.

## GTS frame sources

**Add GTS…** loads a loose `.gts` definition for authoring. Its logical
tilesheet name is stored separately from its source path.

The source tree expands:

```text
Tilesheet
  Region
    Row
      Frame
```

Frame nodes use aspect-fit nearest-neighbor thumbnails. Double-click a frame, or
use **Assign frame**, to apply its logical tilesheet/region/X/Y reference to the
selected scene tile. The GSCN runtime still resolves that reference through the
tilesheet registry.

## GANI animation sources

**Add GANI…** loads a loose `.gani` definition and records its logical
animation key plus authoring path. Double-click an animation, or use **Assign**,
to set the selected tile's `AnimationKey`.

Assigning a key does not redundantly force `EnableAnimator`; GSCN runtime
materialization already treats an `AnimationKey` as sufficient to create the
animator. `StartAnimation` remains a separate tile property.

The preview uses the first GANI frame as a representative image. It does not run
the game animation timer.

## Portable authoring dependencies

Runtime GSCN references remain logical:

```text
frame  -> Tilesheet + RegionName + XTile + YTile
GANI   -> AnimationKey
```

For tooling, the root definition also carries:

```text
TilesheetSources
AnimationSources
```

Those collections locate the corresponding GTS/GANI authoring files. Loose
paths are stored relative to the GSCN whenever possible and are rebased by
**Save As**. Packed GAF source metadata is preserved, although the initial
editor does not preview packed GTS/GANI definition entries.

For older GSCN files that have logical references but no authoring-source
metadata, the editor performs conservative same-directory recovery:

- search is not recursive;
- a source is recovered only when exactly one same-directory GTS/GANI definition
  has the required logical name/key;
- ambiguous matches produce a warning instead of guessing;
- recovered source metadata marks the document dirty so saving migrates it.

## Preview

The preview composes visible layers by `ZOrder`, uses loaded GTS images for
explicit frame references, and uses the first frame of a loaded GANI for
animation-key tiles.

Clicking the preview chooses a tile in the currently selected SceneLayer. The
Tile properties pane and X/Y selector follow that selection.

The preview is an authoring aid, not a replacement for runtime rendering. It
does not execute effects, collision simulation, camera/view behavior, runtime
animation timing, or a game loop.

## Validation

`SceneDefinitionValidator` handles structural GSCN validation. The editor adds
non-destructive authoring diagnostics for unresolved GTS/GANI sources and
preview limitations.

An invalid definition can still be explicitly saved after confirmation, matching
the other standalone Gondwana tooling.

## Studio reuse

Studio should host `SceneEditorControl` directly. It should not reimplement
scene editing or translate GSCN into an older Studio-specific scene model.

The outer Studio shell owns projects/documents/global tools. Each
`SceneEditorControl` owns its editor-local panes and GSCN authoring state.
