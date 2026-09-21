# Gondwana Tilesheets (WinForms)

Standalone .NET 8 Windows editor for `.gts` definitions backed by loose images or image entries in Gondwana asset packages (`.gaf` / `.zip`). Open this project in
Visual Studio, or run from the repository root:

```console
dotnet run --project Tooling/Gondwana.Tooling.Tilesheets.WinForms -c Release
```

## Workflow

- **File → Open working directory** selects the workspace; **F5** refreshes it.
  Subdirectories load on expansion. Referenced images appear beneath their GTS,
  even when names differ. A “same name” child is only a browsing hint.
- Select a GTS to open it. Double-click a loose image (or use its context menu) to
  create a definition. The image context menu also replaces the active document's image.
- Asset packages (`.gaf` / `.zip`) appear in the same project-source tree. Expand a
  package to browse entries stored as `AssetTypes.Image`. Double-click a packed image
  to create a GTS from it, or use the context menu to assign it to the active GTS.
  The preview reads the image directly from `AssetsFile`; it is not extracted to disk.
- The preview toolbar also exposes **GAF image…** for choosing an asset package and
  image entry directly. **Reload image** invalidates the package cache before re-reading
  a packed source.
- Documents and the **Project sources** workspace can be docked, floated, resized and rearranged.
  **View → Project sources** restores the outer workspace if hidden. The same **View**
  menu can restore any hidden pane in the active GTS editor, or **Show all tilesheet panes**.
- **Definition**, **Region**, and **Frame** are stacked in the side inspector,
  all visible together. Drag the horizontal dividers to resize each section;
  each property list scrolls independently.
  Values commit on Enter or focus change. Invalid numeric text stays in the
  property editor instead of reaching the model.
- Select a region from the region list; use **+ Region** / **− Region** to manage
  regions and the **Name** property to rename them. Every geometry edge is editable.
- Click a frame in any region, or use the **Frame X / Y** navigator, to inspect a derived
  coordinate. The frame inspector's XTile/YTile values are read-only.
  Collision adjustment mode and collision type have independent inheritance choices.
  Clicking another region selects that region automatically. For overlapping grids,
  the currently selected region takes precedence. Margin/padding pixels are not frames.
  Edit text/numbers in the value column and press Enter or leave the field to commit;
  use the drop-down for booleans and collision choices. The edited field stays selected.
- Choose 25%, 50%, 100%, 200%, 400%, or Fit; +/- also change zoom. Scrollbars preserve
  image coordinates. Hold **Ctrl** and scroll the mouse wheel over the image viewport
  to zoom in/out using the same steps as +/−. Without Ctrl, the wheel scrolls normally.
  **Overlays / legend** toggles the individual overlays.
  Each row has a color swatch and **...** button that opens a color picker.
  Colors apply immediately to all open documents and persist in
  `Gondwana.Tooling.Tilesheets.WinForms.settings.json` beside the executable.
  The file is created on the first color change and stores colors as `#RRGGBB`.
  Selected-region and selected-frame colors can be customized separately.
  Invalid settings fall back to defaults with a warning in the status panel;
  a failed settings write reports an error and leaves the previous color in place.
- **Ctrl+S**, **Ctrl+Shift+S**, and **Ctrl+W** save, save as, and close. Closing a
  dirty document or the app prompts to save, discard, or cancel.

## Preservation contract

`Editing/TilesheetDocument.cs` holds the complete `TilesheetDefinition`. The UI
mutates individual properties; it does not reconstruct definitions from controls.
Opening, inspecting frames, or selecting a region never materializes missing frame
records or changes inheritance. Explicit overrides equal to region defaults remain
explicit. Provenance is informational and follows the existing serializer policy.

Geometry changes keep frame metadata keyed to its original coordinates. Metadata
outside the new grid remains in the model and produces validator errors. Restore
the geometry to recover it, or explicitly confirm **Prune invalid frames** to delete
it. No metadata is automatically moved or discarded.

Saving uses `TilesheetDefinitionSerializer` on a full snapshot, then replaces the
destination with the completed temporary file. A failed save leaves the in-memory
document dirty. Save As rebases image references (including preserved packed file
references) to maintain their targets. Ordinary Save preserves existing relative
path spelling; absolute image paths are made relative where possible. Editor state
is never serialized. Unknown JSON fields outside the current model follow the
official serializer's behavior and are not retained.

The shared `TilesheetDefinitionValidator` owns layout and collision validation.
The document adds loose-file existence/source checks and the UI adds decoding
diagnostics and image dimensions. Errors and warnings appear below the image.
Saving invalid data requires an explicit **Save anyway** decision.

## Preview and first-pass limits

- WinForms/GDI+ handles rendering. Skia is used only to decode the same image
  formats as the runtime and apply its mask helper. No Engine, GameHost, Scene,
  SceneLayer, runtime tilesheet, or registry is created by the editor.
- Color-key preview follows the current runtime's RGB tolerance matching. Mask
  Alpha is preserved and editable, although the runtime ignores it when matching.
  PremultiplyAlpha is preserved independently; GDI+ composites transparency for
  display rather than simulating the engine's complete rendering pipeline.
- Overhang is a world-space setting. Its overlay illustrates outward extents at
  source pixel scale, not additional pixels to extract from the image. Collision
  overlays use `CollisionAdjust.ApplyTo` and show geometry even for `None` types.
- At extreme grid densities, the preview samples cells to keep painting bounded.
  The selected cell is always drawn and every frame remains accessible by X/Y.
- Packed image sources are previewed and validated through `AssetsFile` using
  `AssetsFilePath` + `AssetEntryName`. Saving keeps the logical package reference
  and rebases the package path relative to the GTS destination where possible; it does
  not extract or duplicate the image. The standalone tool can prompt for an encrypted
  package password, but that password is tooling state and is never written into GTS.
- Native file dialogs, window chrome and scrollbar parts follow the Windows theme.
  Editor surfaces and docking use the dark theme.
- No undo/redo, external-file watching, animations, tile maps, or mouse-based
  resizing/collision editing in this pass. Use directory refresh and Reload image.

## Reusable controls and Studio integration

The standalone executable is intentionally a host around reusable editor controls:

```text
MainForm / DockPanelSuite
    |
    +-- TilesheetWorkspaceControl
    |
    +-- EditorDocument (thin DockContent wrapper)
            |
            +-- TilesheetEditorControl
                    |
                    +-- TilesheetDocument
```

`TilesheetEditorControl` and `TilesheetWorkspaceControl` are public WinForms
`UserControl` types. `EditorDocument` contains standalone docking/close-prompt
behavior only. A future Gondwana Studio WinForms host can therefore place the same
controls inside its own document/tool-window model instead of copying the standalone
forms.

`AssetPackageCatalog` is a non-visual package/image service that can be shared by
multiple hosted controls. The GTS standalone app shares one catalog between its
workspace and open editors; Studio can do the same while eventually composing it
with the hostable GAF editor.

## Validation and maintenance

The UI-independent editing files and scalar property adapters are linked into
`Testing/Gondwana.Tests`. `TilesheetEditorDocumentTests` covers complete semantic
round trips, inherited vs explicit values, packed-field preservation, Save As
rebasing, failed/invalid saves, geometry changes, mask removal, and runtime geometry
parity. Run the repository's normal Release build and full regression suite.

For Windows UI verification, open two documents, modify properties, exercise each
zoom and scroll position, dock/float the workspace, and cancel a dirty close.
Verify that selecting an inherited frame does not mark a document dirty, and that
disabling its inheritance does mark it dirty even if the value equals the default.

Windows interaction regressions also run in CI and can be run locally:

```console
dotnet test Testing/Gondwana.Tooling.Tilesheets.WinForms.Tests -c Release
```

These tests open all five Zelda sample definitions, click frames across their regions,
and exercise actual PropertyGrid textbox commits, focus changes and dropdown choices
for both existing GTS documents and definitions created from images.

## Editor-local docking

TilesheetEditorControl remains a reusable WinForms UserControl. Each instance owns an
inner DockPanelSuite surface with these panes: Image, Definition, Region, Frame, and Validation.
The standalone application still hosts the entire editor as **one outer document**;
the working-directory browser and other documents belong to the outer DockPanel.
An embedding host does not need to create or manage the internal panes.

The default layout follows the previous editor arrangement. Panes can be docked,
tabbed, and split inside their own editor. All inner panes use document-style
groups with tabs at the top, including panes initially placed at the sides or
bottom. Floating and auto-hide are disabled for inner panes.
Closing a pane using its close button hides it without closing the document.
Use **View** to restore an individual pane in the active GTS document, or choose
**Show all tilesheet panes**. A restored pane returns to its previous split/tab
group for that open document. Layouts, including hidden panes, persist per editor type and entry application.

The small helper in Tooling/Shared/WinForms/EditorDockWorkspace.cs is source-linked
by the standalone WinForms tooling projects, so they do not depend on one another. It owns the
inner dock contents (including hidden panes) and theme, and disposes them with the
editor. Existing editor-specific model, image, preview, and event cleanup remains
in each editor. Hosts should dispose the whole editor when closing its document.

Nested docking, ownership, hide/dispose/reopen, and GAF filter/save coverage runs
in Testing/Gondwana.Tooling.Tilesheets.WinForms.Tests as part of the existing
Windows CI job; GANI's existing interactions remain in its animation test project.

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
no source-document paths or unsaved content are stored, and documents are never reopened
on startup. Missing or corrupt preferences fall back to defaults without a dialog.
Available plugin tools in Studio use stable plugin identities; missing plugins are ignored.
Changes are saved after a short settling interval and flushed on disposal.
