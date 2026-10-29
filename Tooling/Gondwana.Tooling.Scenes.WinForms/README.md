# Gondwana Scenes (WinForms)

The standalone shell opens loose definition documents. To open and save a definition
inside a GAF, use [Gondwana Studio](../Gondwana.Tooling.Studio.WinForms/README.md#editing-packed-definitions),
which hosts this same editor. Packed documents retain all logical members and their
source references; Save updates the typed archive entry, while Save As adopts a loose copy.

Standalone .NET 8 Windows editor for Gondwana `.gscn` scene definitions.

Run from the repository root:

```console
dotnet run --project Tooling/Gondwana.Tooling.Scenes.WinForms -c Release
```

## Undo and redo (Phase 1)

**Edit > Undo** / **Ctrl+Z** and **Edit > Redo** / **Ctrl+Y** operate on the
active GTS or GSCN document in both Studio and the standalone editors.
Ctrl+Shift+Z also redoes. Textboxes and property-grid edit fields retain local
text-editing shortcuts; commit the value before using document history.
Selection, zoom, and preview-only visibility are not document edits.

Undoing back to the last successful save clears the dirty marker; undoing past
that save makes it dirty again. Save and Save As retain history and establish a
checkpoint only after success. New unsaved documents remain dirty. Editing after
Undo discards the redo branch. Each document retains up to 200 committed operations
by default; evicting history never makes an unreachable saved state clean.

History includes in-memory edits to loose and GAF-backed GTS/GSCN definitions.
It cannot reverse committed archive relocation, file deletion, or cross-file
reference rewrites. GANI, GSPR, GSND, and general GAF history are deferred to
later phases. Scene painting and fill tools are not included in this phase.

GSCN history covers members, layers, property and collection edits, source references,
and sparse tile frame/animation, transform, and collision edits. Undoing an edit that
created a tile removes that tile again. Bulk operations can use a grouped transaction;
Undo reverses the group in reverse order, and cancelling restores its starting state.


## Design

The editor is definition-driven. It edits `SceneDefinition` directly and does
not boot a `GameHost`, register an editor scene in the global runtime scene
collection, or require rendering/input hosts simply to author GSCN.

`SceneEditorControl` is a public, hostable WinForms `UserControl`. The
standalone application and Gondwana Studio host the same control.

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

- **Scene structure** — scene/layer navigation, layer add/remove commands, and
  preview-only visibility checkboxes for individual layers or the entire scene.
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

Layouts, including hidden panes, persist per editor type and entry application.

## Sparse tile editing

GSCN supports sparse tile definitions. Merely selecting a grid cell does not add
a `SceneLayerTileDefinition`.

The editor creates a tile definition only when the user changes a tile property,
assigns a GTS frame, or assigns a GANI animation. **Clear tile** removes that
explicit entry and returns the cell to GSCN defaults.

This keeps large mostly-empty layers compact.

## GTS frame sources

**Add from GAF…** in either source pane selects an exact typed GTS or GANI entry
and a logical member from a GAF/ZIP package. It validates the selected definition
and rejects conflicting logical source names. Existing frame previews and
representative animation frames use the selected packed dependencies.

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
to set the selected tile's `AnimationKey` and initial `Frame` to the first frame
of that animation. This replaces an existing frame assignment; if no GANI frame
is available, the existing frame is left intact.

Assigning an animation also sets `EnableAnimator` and `StartAnimation` to
`true` so the assigned cycle starts when the scene is materialized. Both flags
can still be changed independently in the tile properties.

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
**Save As**. Packed GTS/GANI dependencies and their packed images are previewed
through the [shared authoring resolver](../Gondwana.Tooling.Assets/README.md).
An animation's explicit tilesheet source is respected even if the scene declares
a different source with the same logical name. Missing packed dependencies produce
warnings instead of falling back to loose duplicates. **Reload sources** refreshes
the loaded definitions and images without changing source metadata or dirty state.

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

Use the checkboxes in **Scene structure** to temporarily show or hide each
layer. The scene-level checkbox toggles all layers; **Show all** and **Hide all**
in the structure toolbar work even when only some layers are checked.
Unchecked layers do not draw, contribute to preview bounds, or accept preview
tile clicks. The initial check state follows each layer's saved `Visible` value,
but these preview toggles do **not** modify `Visible`, mark the document dirty,
or affect a separately launched runtime Scene Viewer.

Clicking the preview chooses a tile in the currently selected SceneLayer. The
Tile properties pane and X/Y selector follow that selection.

The preview uses the same zoom interaction as the GTS editor:

- **Ctrl+mouse-wheel** zooms in/out in 1.25× steps.
- **−** and **+** in the preview toolbar use the same 1.25× step.
- The zoom selector provides 25%, 50%, 100%, 200%, 400%, and **Fit**.
- Horizontal and vertical scrollbars appear when the zoomed scene exceeds the
  preview viewport.
- **Grid** toggles preview grid lines without hiding the selected-tile outline.

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

## Dock layout preferences

DockPanelSuite layouts are saved per user under
`%LOCALAPPDATA%/Hidden Worlds Games/Gondwana/Tooling/<entry-application>/Docking/`.
Outer tool windows and each editor type (GAF, GTS, GANI, GSND, GSCN) have separate
profiles. Dock locations, tabs, splits, relative sizes, and hidden-pane visibility
survive restart. Studio and the standalone executables use independent profiles.
New documents use their editor type's last saved arrangement, regardless of file path.

Use **View → Reset application layout** to restore the outer tool arrangement, or
**View → Reset active editor layout** to restore the active editor type's defaults.
Individual View commands and **Show all … panes** recover hidden panes without resetting.
Reset does not change document data. Open sibling editors keep their current layouts;
the next newly opened editor uses the reset profile until another layout is changed.

These are UI preferences only: no Gondwana content files or EngineState are modified,
the dock XML stores no source-document paths or unsaved content. Document reopening
is handled by the separate workspace-session preference. Missing or corrupt preferences fall back to defaults without a dialog.
Available plugin tools in Studio use stable plugin identities; missing plugins are ignored.
Changes are saved after a short settling interval and flushed on disposal.

## Tile orientation

Use **Appearance > Transform** to choose one of the eight native orthogonal
orientations. The tile toolbar also offers **Rotate left 90 degrees**, **Rotate right
90 degrees**, **Flip horizontal**, and **Flip vertical**. Operations compose in placement
space; Clear tile still removes the entire sparse placement.

Geometry shows source and effective size, overhang, and padding; Collision shows
the effective oriented adjustment, including frame overrides when following a
frame. Preview orients artwork and accounts for its overhang, while the gold
outline continues to mark the fixed grid cell. Source GTS thumbnails and source
atlas slicing are unchanged. GSCN stores the orientation per tile, with Identity
as the default for older documents.

## Scene Preview and View Scene

**Scene Preview** remains lightweight, editable, and definition-driven. It does not run an Engine and uses a representative/static GANI frame where applicable.

**View Scene**, on the preview toolbar, launches the separate [Scene Viewer](../Gondwana.Tooling.SceneViewer.WinForms/README.md). It loads GSCN content (or a temporary snapshot) and its explicit dependencies into a real Gondwana Scene, with actual Engine rendering, runtime frame transformations, real GANI playback, and camera navigation. New or dirty **loose** scenes must be saved first; canceling the save prevents launch. Packed scenes instead launch a temporary snapshot of the current edits without saving or adopting the packed document.

The command is part of `SceneEditorControl`, so it also works when hosted in Studio. Hosts supply `SaveRequested` to reuse their normal save path. Build the viewer in the same configuration as the editor, or deploy its complete output under `SceneViewer/` beside the editor executable. See the viewer README for all supported layouts, controls, and current GSPR association limitations.

## Workspace sessions

This standalone application remembers its working directory, saved documents,
tab order, and active document independently of Studio and the other editors.
**File > Restore workspace on startup** controls restoration. **Start empty on
next launch**, holding Shift at launch, or `--empty-workspace` bypasses it.
Unsaved contents and untitled documents are not recovered. Missing/invalid files
are skipped with a startup summary. See [workspace session restoration](../Gondwana.Tooling.Studio.WinForms/README.md#workspace-session-restoration)
for storage, close behavior, encrypted archives, and recovery details.

## Appearance

Choose **View → Theme → Dark, Dark Blue, or Light**. The choice is shared across Studio and all six standalone editors and takes effect on the next launch. Open documents remain untouched; restart normally after saving your work. See [shared tooling themes](../Shared/WinForms/Theming/README.md) for persistence, control coverage, and DockPanel Suite limitations.
