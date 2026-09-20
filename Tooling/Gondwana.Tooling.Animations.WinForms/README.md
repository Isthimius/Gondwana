# Gondwana Animations (WinForms)

Standalone .NET 8 Windows editor for Gondwana `.gani` animation definitions.

Run from the repository root:

```console
dotnet run --project Tooling/Gondwana.Tooling.Animations.WinForms -c Release
```

## Design

The editor is definition-driven. It edits `AnimationDefinition` directly and never
boots a `GameHost`, creates a runtime `Animator`, or serializes runtime playback
state.

`AnimationEditorControl` is a public, hostable WinForms surface. The standalone
application places that control inside a DockPanelSuite document, but a future
Gondwana Studio host can reuse the same editor surface without depending on
`MainForm`.

## Workflow

- **File → New** creates a GANI definition with a repeating cycle and 0.1-second
  throttle.
- **File → Open GANI** opens one or more `.gani` documents.
- **File → Add GTS source** loads one or more loose `.gts` definitions as authoring
  sources for the active animation and records those dependencies in the GANI definition.
- Reopening a GANI automatically reloads its recorded loose GTS sources. Legacy GANI
  files without source metadata can recover exactly one matching sibling GTS by logical
  tilesheet name and are then marked dirty so the dependency is persisted on save.
- The left source browser shows tilesheets, regions, rows and frames lazily. Expand
  a row and single-click a frame to preview it; double-click the frame or its preview
  image to append it. The **Add** action performs the same append explicitly.
- The sequence panel provides **Add**, **Remove**, **Up**, and **Down** actions.
- The property panel edits the GANI key, throttle, cycle type, hide-on-end flag and
  next-cycle key.
- **Play/Pause**, **Restart**, and **Step** preview the current sequence. Fit, 1x, 2x
  and 4x preview scales use nearest-neighbor sampling.
- Structural errors come from `AnimationDefinitionValidator`. Missing or
  unresolvable loaded GTS sources appear as warnings because a GANI file is allowed
  to reference content that is not currently open in the editor.
- **Ctrl+S**, **Ctrl+Shift+S**, and **Ctrl+W** save, save as and close. Dirty documents
  prompt on close.

## GTS dependency model

GANI keeps runtime frame identity logical:

```text
Tilesheet name
Region name
X/Y frame coordinates
```

It also persists lightweight `TilesheetSources` metadata so tooling can find the
related GTS again. Loose GTS paths are saved relative to the GANI location whenever
possible and are rebased by Save As. The source metadata does not embed the GTS or
its image, and runtime `ToCycle` materialization still resolves frames only through
`TilesheetRegistry`.

The dependency metadata is also shaped to represent packed GTS entries
(`AssetsFilePath` + `AssetEntryName`) for future editor support. Packed references
are preserved today, but this editor currently previews loose GTS sources only.

The editor refuses to load two GTS files with the same logical
`TilesheetDefinition.Name` into one document because the resulting GANI references
would be ambiguous.

Loose GTS image files are decoded with Skia and displayed through WinForms/GDI+.
Mask metadata is applied for preview parity. Packed image references remain valid
authoring references but do not have a preview in this first pass; later GAF tooling
integration can extend that path.

## Studio direction

The standalone executable is intentionally only one host:

```text
MainForm / DockPanel
        |
        v
AnimationEditorControl
        |
        v
AnimationDocument + AnimationDefinition
```

Studio can eventually host `AnimationEditorControl` directly while using its own
project/reference model around it.

## Editor-local docking

AnimationEditorControl remains a reusable WinForms UserControl. Each instance owns an
inner DockPanelSuite surface with these panes: GTS frame sources, Preview, Animation frames, Animation properties, and Validation.
The standalone application still hosts the entire editor as **one outer document**;
the working-directory browser and other documents belong to the outer DockPanel.
An embedding host does not need to create or manage the internal panes.

The default layout follows the previous editor arrangement. Panes can be docked,
tabbed, and split inside their own editor; floating is disabled for inner panes.
Closing a pane using its close button hides it without closing the document.
Close and reopen the document to recover the default panes and arrangement.
Layouts reset to their defaults when documents are opened. Layout persistence is
intentionally not implemented: no layout XML, settings, or registry state is saved.

The small helper in Tooling/Shared/WinForms/EditorDockWorkspace.cs is source-linked
by the three tooling projects, so they do not depend on one another. It owns the
inner dock contents (including hidden panes) and theme, and disposes them with the
editor. Existing editor-specific model, image, preview, and event cleanup remains
in each editor. Hosts should dispose the whole editor when closing its document.

Nested docking, ownership, hide/dispose/reopen, and GAF filter/save coverage runs
in Testing/Gondwana.Tooling.Tilesheets.WinForms.Tests as part of the existing
Windows CI job; GANI's existing interactions remain in its animation test project.
