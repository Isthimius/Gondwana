# Gondwana Asset Files (WinForms)

Standalone .NET 8 Windows editor for Gondwana asset files (`.gaf` / `.zip`). It provides a
dark, dockable desktop workspace for creating, inspecting, importing, replacing, exporting,
renaming, and deleting assets stored through `AssetsFile`.

Open this project in Visual Studio, or run it from the repository root:

```console
dotnet run --project Tooling/Gondwana.Tooling.Assets.WinForms -c Release
```

This is development tooling, not a runtime dependency for a game.

To edit a native definition inside a GAF, use [Studio's Working Directory](../Gondwana.Tooling.Studio.WinForms/README.md#editing-packed-definitions).
Studio opens each typed entry in its corresponding editor and saves it back without
extraction. When the parent GAF and its definitions are open together, save the
parent's pending changes first. Conflicting entry changes require reload or Save As;
Studio never silently replaces another editor's unsaved work. The standalone GAF
editor continues to manage container entries through its import/replace/export tools.

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
- **Add / Import** reviews each file's inferred type and entry name, offers dependency import or external-reference preservation, and previews the prepared changes before applying them.
- **Replace** keeps the selected asset's type and name while replacing its stored
  contents from another file. This is byte replacement, not semantic import; it does not validate inbound relationships or repair removed logical members.
- **Rename / Move…** reviews the search scope and inbound/outbound reference updates, then commits a recoverable disk transaction.
- **Export** writes one complete entry to a loose file. Native documents are rebased through the shared semantic export planner; omitted dependencies stay packed. Save a dirty archive before exporting native definitions. Double-clicking a row performs the same export. The source archive and dirty state are unchanged.
- **Delete** reviews inbound references before committing removal; surviving references block deletion unless explicitly authorized.
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
- Add / Import and Replace modify the current in-memory archive; use **Save** to persist them. Rename / Move and Delete commit to disk after review, then refresh the borrowed archive and leave it clean. Save the archive and close other authoring documents before those operations.
- The shared control exposes `IsDirty` and `Changed`; document hosts own close/save routing. Save pending changes before closing the standalone editor.

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
for that open document.
Layouts, including hidden panes, persist per editor type and entry application.

The small helper in Tooling/Shared/WinForms/EditorDockWorkspace.cs is source-linked
by the standalone WinForms tooling projects, so they do not depend on one another. It owns the
inner dock contents (including hidden panes) and theme, and disposes them with the
editor. Existing editor-specific model, image, preview, and event cleanup remains
in each editor. Hosts should dispose the whole editor when closing its document.

Document hosts such as Studio can observe `IsDirty` / `Changed` and route the
toolbar through the asynchronous `SaveRequested` callback. When that callback is
set, direct synchronous `SaveTo(path)` is rejected so it cannot bypass the host's
archive coordination. Studio stages its saves and calls `AcceptSavedArchive` only
after persistence succeeds. Without a host callback, `SaveTo(path)` saves and
adopts that path while preserving encryption; standalone Save As keeps its existing save-copy behavior.
The caller owns the originally supplied `AssetsFile`; the editor owns and disposes
replacement packages created by `SaveTo`. Hosts making direct package changes
should call `MarkChanged`. Authoring hosts can load packages with
`AssetsFile.LoadOrCreate(path, password, encrypt, register: false)` to keep them
outside the runtime asset registry.

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
the dock XML stores no source-document paths or unsaved content. Document reopening
is handled by the separate workspace-session preference. Missing or corrupt preferences fall back to defaults without a dialog.
Available plugin tools in Studio use stable plugin identities; missing plugins are ignored.
Changes are saved after a short settling interval and flushed on disposal.

## Semantic GAF Add / Import

The standalone Assets editor and Studio use the same `AssetEditorControl` workflow:

1. Select one or more loose files in **Add / Import**.
2. Review the proposed type and destination entry name for each file. Common media types are inferred individually. GTS, GANI, GSCN, GSPR, and GSND types are fixed to their native definition types; unknown files default to Misc and can be corrected.
3. Choose **Import dependencies** to copy the known recursive dependency closure, or **Keep external references** to preserve dependencies outside the destination GAF. Explicitly selected files are still imported in either mode.
4. Review additions, reused entries, physical reference transformations, and remaining external dependencies. **Import** applies the prepared plan; **Cancel** leaves the archive and dirty state unchanged.
5. **Save** persists the changes. Import does not save automatically, move/delete source files, or change the borrowed archive instance or its password/encryption settings.

Each native file remains one document containing all its logical members, in order. Member identities, animation assignments, scene relationships, and URI audio are preserved. References use the current destination GAF path; externally retained paths are rebased relative to its directory. Known dependencies in subdirectories retain paths relative to the first selected file's directory. Dependencies outside that directory use `dependencies/<AssetType>/<filename>`; resources copied from another GAF use `dependencies/<archive-name>/<entry-name>`. All names are shown in the preview.

Existing entries are resolved against the current in-memory archive, including unsaved edits. Exact typed packed references can reuse existing members; logical-only references require a unique member among selected/current documents. A leaf resource at the planned exact typed entry is reused only when its bytes are identical. Reused native documents are checked recursively against their existing references; any external dependencies they already have remain external and appear in the preview, even with Import dependencies selected. Basename matches alone never establish equivalence. Missing/ambiguous sources and conflicting native documents fail clearly. Existing entries are never replaced by Add / Import; change the destination name or supply an explicit source. This workflow does not certify the safety of the separate existing **Replace** command.

Only explicit physical dependencies are followed recursively; the importer does not scan neighboring folders or arbitrary project code to guess missing source metadata. Include the relevant files in the selection or author explicit sources. Referenced external archives use the destination password when opening is required; archives requiring a different password are not supported by this workflow. URI resources are not downloaded.

The UI-independent `Gondwana.Tooling.Assets` library owns the dependency analysis shared by CLI and editors, using Core document serializers and relocation. It buffers a snapshot and all prepared additions before applying them. A stale preview is rejected if archive contents changed. Cancellation or an ordinary multi-entry write failure removes attempted additions, preserving unrelated edits. Apply on the editor thread; this does not provide concurrent-writer synchronization or recovery from process termination/out-of-memory failure.

Add / Import remains copy behavior. Reference-aware Move, portable Save As, and reference-aware Delete are described below.

## Reference-aware Rename / Move

Use **Rename / Move…** for packed entry renames, moves within or between GAFs, or moves to loose storage. Review the explicit search scope, destination, per-archive credentials, and affected-document preview before confirming. Save the current archive and close other editors first. Successful moves are committed to disk and refresh the same borrowed archive; ordinary failure/cancellation rolls back the operation. Save As now rebases outbound references for the copied archive while preserving the original and its inbound references; choose a new destination.

See [Assets Files — reference-aware moves](https://github.com/Isthimius/Gondwana/wiki/Assets-Files#reference-aware-rename-and-move) for all storage transitions, scope boundaries, encryption, rollback and interrupted-commit recovery. Outside-scope references and arbitrary C# are not rewritten. Existing pack/unpack semantics are unchanged.

## Reference-aware Delete

**Delete** reviews the explicit scope and all direct surviving references before touching an entry. Referenced entries are blocked by default. **Delete anyway…** requires a second confirmation to leave precisely the listed references dangling; dependents remain unchanged. Save the current archive and close other authoring editors first. Success commits to disk and refreshes the same borrowed archive, leaving it clean; canceled or blocked operations do not change dirty state. Removing the last entry retains the archive.

See [Assets Files — reference-aware deletion](https://github.com/Isthimius/Gondwana/wiki/Assets-Files#reference-aware-deletion) for scope, credentials, batching, force semantics, and recovery guarantees.

## Workflow and safety reference

[Assets Files](https://github.com/Isthimius/Gondwana/wiki/Assets-Files) describes physical files, typed entries, logical members, and copy versus move. [Reference Relocation](https://github.com/Isthimius/Gondwana/wiki/Reference-Relocation#shared-tooling-and-safety) owns the shared scope, credentials, rollback/recovery guarantees, and deferred capabilities. Move and Delete share scope terminology, per-archive credential prompts, and a read-only preview whose default action is Cancel; forced Delete retains its separate consequence confirmation.

## Workspace sessions

This standalone application remembers its working directory, saved documents,
tab order, and active document independently of Studio and the other editors.
**File > Restore workspace on startup** controls restoration. **Start empty on
next launch**, holding Shift at launch, or `--empty-workspace` bypasses it.
Unsaved contents and untitled documents are not recovered. Missing/invalid files
are skipped with a startup summary. See [workspace session restoration](../Gondwana.Tooling.Studio.WinForms/README.md#workspace-session-restoration)
for storage, close behavior, encrypted archives, and recovery details.
