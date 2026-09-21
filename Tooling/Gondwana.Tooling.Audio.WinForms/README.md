# Gondwana Audio (WinForms)

Standalone .NET 8 Windows editor for Gondwana `.gsnd` audio definitions.

Run from the repository root:

```console
dotnet run --project Tooling/Gondwana.Tooling.Audio.WinForms -c Release
```

## Design

The editor is definition-driven. It edits `AudioDefinition` directly and does not
boot a `GameHost` or require a configured playback backend simply to author GSND.

`AudioEditorControl` is a public, hostable WinForms surface. The standalone
application places that control inside a DockPanelSuite document; Gondwana Studio
can host the same control later without copying editor logic.

## Workflow

- **File → New** creates an empty GSND definition.
- **File → Open GSND** opens one or more `.gsnd` documents.
- **Add file** adds a loose audio source and assigns a unique default key.
- **Add URI** adds a URI-backed audio source.
- The resource Properties pane edits key, source metadata, volume, pan, playback
  speed, and looping.
- Validation uses `AudioDefinitionValidator`.
- **Ctrl+S**, **Ctrl+Shift+S**, and **Ctrl+W** save, save as, and close.
- Loose file and GAF references are rebased relative to the GSND file when possible.

The GSND core model also supports packed GAF audio references
(`AssetsFilePath` + `AssetEntryName`). These can be edited directly in the
property pane; a dedicated packed-audio picker is intentionally outside this
initial small editor pass.

## Editor-local docking

Each `AudioEditorControl` owns an inner DockPanelSuite workspace containing
**Audio resources**, **Properties**, and **Validation**. Panes can split or tab
inside that editor only. Floating and auto-hide are disabled by the shared
`EditorDockWorkspace` helper.

Closing a pane hides it without closing the GSND document. Use **View** to restore
an individual pane, or choose **Show all audio panes**. **View → Working directory**
restores the outer workspace. A restored inner pane returns to its previous
split/tab group for the current document.

Layouts persist per editor type and entry application. The standalone application's
Working directory and each whole GSND editor remain outer docked windows/documents.

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
