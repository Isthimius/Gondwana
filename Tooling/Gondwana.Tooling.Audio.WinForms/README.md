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

Layout persistence is intentionally not implemented. The standalone application's
Working directory and each whole GSND editor remain outer docked windows/documents.
