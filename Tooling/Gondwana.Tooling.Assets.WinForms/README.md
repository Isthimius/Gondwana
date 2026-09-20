# Gondwana Asset Files (WinForms)

Standalone .NET 8 Windows editor for Gondwana asset files (`.gaf` / `.zip`). It provides a
dark, dockable desktop workspace for creating, inspecting, importing, replacing, exporting,
renaming, and deleting assets stored through `AssetsFile`.

Open this project in Visual Studio, or run it from the repository root:

```console
dotnet run --project Tooling/Gondwana.Tooling.Assets.WinForms -c Release
```

This is development tooling, not a runtime dependency for a game.

## Workspace

The editor follows the same WinForms workspace model as the Gondwana Tilesheets editor:

- **File → Open working directory** selects the directory shown in the **Asset files**
  tree. **F5** refreshes it, and subdirectories load as they are expanded.
- The tree shows `.gaf` and `.zip` asset containers. Selecting a file opens it.
- **File → Open** can open several asset files at once.
- Every open asset file has its own document tab and independent filters, grid, status,
  and in-memory `AssetsFile` instance.
- Documents and the **Asset files** workspace can be docked, floated, resized, and
  rearranged. **View → Asset files** restores the outer workspace if it is hidden.
  The same **View** menu restores **Assets** or **Status** in the active GAF editor,
  and **Show all asset panes** restores both.
- The editor surfaces use the same VS2015-style dark docking theme as the Tilesheets
  tool. Native Windows file dialogs and window chrome continue to follow Windows.

## Reusable controls and Studio integration

The standalone application shell no longer owns the asset-editing UI directly.
The reusable pieces are:

```text
MainForm / DockPanelSuite
    |
    +-- AssetWorkspaceControl
    |
    +-- AssetEditorDocument (thin DockContent wrapper)
            |
            +-- AssetEditorControl
                    |
                    +-- AssetsFile
```

`AssetEditorControl` and `AssetWorkspaceControl` are public WinForms
`UserControl` types. The DockPanelSuite document remains an application-shell
adapter and owns disposal of the `AssetsFile` in the standalone tool.

This mirrors the GTS/GANI direction so Gondwana Studio can host the same editing
surfaces rather than maintaining copied versions of the tools. Host-specific concerns
such as which files are already open and when a workspace should refresh are exposed
as callbacks instead of being hard-wired to `MainForm`.

## Asset-file workflow

- **New** creates a new `.gaf` (or `.zip`) asset file. New files can be created
  either unencrypted or password-protected and open immediately in their own document.
- **Open** loads one or more existing asset files. If the initial open fails, the editor
  prompts for a password and retries the file as an encrypted asset container.
- The document grid lists each asset's **Type**, **Name**, and stored **Size**.
- Use the **Type** selector to show all entries or only one `AssetTypes` value.
  The **Search** box filters the current list by asset name as you type.
- **Add / Import** imports one or more files. Choose the Gondwana asset type once
  for the selected files, then confirm or change the asset name for each file.
- **Replace** keeps the selected asset's type and name while replacing its stored
  contents from another file.
- **Rename** copies the selected asset to the new name and removes the old entry.
- **Export** writes the selected asset back to a normal file. Double-clicking a
  grid row performs the same export operation.
- **Delete** removes the selected asset after confirmation.
- **Refresh** rebuilds that document's grid from its currently loaded asset file and
  reapplies the current type and search filters.
- **Save** persists the active asset file. **Save As** writes a complete copy to
  another `.gaf` or `.zip`, with an independent choice of password protection.
- **Ctrl+S**, **Ctrl+Shift+S**, and **Ctrl+W** save, save as, and close the active
  document.

Each document's status bar reports its file path and filtered asset count, along with
the result of operations such as import, rename, export, and save.

## Asset types

Entries are stored using Gondwana's `AssetTypes` classification rather than being
inferred solely from file extensions. The editor exposes those types directly when
importing and when filtering the grid.

This allows an asset file to contain heterogeneous content while retaining the
runtime type information expected by Gondwana. For example, SVG content is stored
as an `Svg` asset entry rather than merely as an arbitrary file with an `.svg`
extension.

## Asset-file behavior

The editor works through Gondwana's `AssetsFile` API. Asset contents are read and
written as streams, so the tooling does not need format-specific editors for the
payload itself.

A few behaviors are worth noting:

- `.gaf` and `.zip` files are both offered by the file dialogs and workspace tree.
- Password protection is chosen when creating a file or when using **Save As**.
- Opening an encrypted file requires the correct password before entries can be
  enumerated.
- **Save As** copies every entry from the currently open asset file into the new
  destination; it does not merely copy the container file on disk.
- Importing, replacing, renaming, and deleting modify the in-memory `AssetsFile`
  owned by that document. Use **Save** to persist those changes to its current file.
- Closing a document does not currently provide dirty-state tracking or a save prompt,
  matching the pre-docking editor's save semantics.

## Current scope

This tool is intentionally an asset-container editor. It manages the assets inside a
Gondwana asset file but does not try to edit the contents of those assets. Image,
audio, SVG, tilesheet, and other payloads remain ordinary stored streams and should
be edited with their appropriate authoring tools before import.

The editor currently provides:

- asset-file creation and multi-file opening
- dockable/floating asset documents
- working-directory tree browsing
- optional password-protected containers
- multi-file import
- Gondwana asset-type assignment
- type filtering and name searching
- asset replacement and renaming
- individual asset export
- asset deletion
- Save and Save As

## Development and maintenance

The UI is implemented directly with WinForms and DockPanelSuite and uses the engine's
existing `Gondwana.Assets` model rather than maintaining a second asset-file
implementation. Changes to this tool should preserve compatibility with `AssetsFile`
and `AssetTypes` instead of introducing editor-only file semantics.

Run the project directly for Windows UI verification and exercise at least these paths
when changing asset-file behavior:

1. Choose a working directory containing multiple `.gaf` files and open several from
   the workspace tree.
2. Dock, float, close, and restore the **Asset files** workspace; rearrange multiple
   open asset documents.
3. Create both plain and password-protected asset files.
4. Import multiple assets and assign a type.
5. Filter by type and search by name independently in two open documents.
6. Replace, rename, export, and delete an entry.
7. Save, reopen, and verify the resulting entries.
8. Use **Save As** both with and without password protection and reopen the copy.

For broader regression coverage, also run the repository's normal Release build and
test suite.

## Documentation

- [Gondwana source repository](https://github.com/Isthimius/Gondwana)
- [Gondwana Wiki](https://github.com/Isthimius/Gondwana/wiki)
- [API Reference](https://isthimius.github.io/Gondwana/api/)

## License

MIT

## Editor-local docking

AssetEditorControl remains a reusable WinForms UserControl. Each instance owns an
inner DockPanelSuite surface with these panes: Assets (entry grid, filters, and commands) and Status.
The standalone application still hosts the entire editor as **one outer document**;
the working-directory browser and other documents belong to the outer DockPanel.
An embedding host does not need to create or manage the internal panes.

The default layout follows the previous editor arrangement. Panes can be docked,
tabbed, and split inside their own editor. All inner panes use document-style
groups with tabs at the top, including panes initially placed at the sides or
bottom. Floating and auto-hide are disabled for inner panes.
Closing a pane using its close button hides it without closing the document.
Use **View** to restore an individual pane in the active GAF document, or choose
**Show all asset panes**. A restored pane returns to its previous split/tab group
for that open document. Layouts reset to their defaults when documents are opened.
Layout persistence is intentionally not implemented: no layout XML, settings, or
registry state is saved.

The small helper in Tooling/Shared/WinForms/EditorDockWorkspace.cs is source-linked
by the standalone WinForms tooling projects, so they do not depend on one another. It owns the
inner dock contents (including hidden panes) and theme, and disposes them with the
editor. Existing editor-specific model, image, preview, and event cleanup remains
in each editor. Hosts should dispose the whole editor when closing its document.

Document hosts such as Studio can observe `IsDirty` / `Changed` and route the
toolbar through `SaveRequested`. `SaveTo(path)` saves and adopts that path while
preserving encryption; standalone Save As keeps its existing save-copy behavior.
The caller owns the originally supplied `AssetsFile`; the editor owns and disposes
replacement packages created by `SaveTo`. Hosts making direct package changes
should call `MarkChanged`. Authoring hosts can load packages with
`AssetsFile.LoadOrCreate(path, password, encrypt, register: false)` to keep them
outside the runtime asset registry.

Nested docking, ownership, hide/dispose/reopen, and GAF filter/save coverage runs
in Testing/Gondwana.Tooling.Tilesheets.WinForms.Tests as part of the existing
Windows CI job; GANI's existing interactions remain in its animation test project.
