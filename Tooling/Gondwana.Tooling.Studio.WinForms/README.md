# Gondwana Studio

Gondwana Studio is the Windows authoring shell for `.gaf` / `.zip` asset
containers, `.gts` tilesheets, `.gani` animations, `.gsnd` sounds, `.gspr` sprite collections, and `.gscn`
scenes. It hosts the same public editor controls as the standalone utilities.
It does not require a GameHost or a running game loop.

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


## Running

```console
dotnet run --project Tooling/Gondwana.Tooling.Studio.WinForms --configuration Release
```

Choose **File > Open working directory…** to browse authoring files. Directories
expand lazily, with files grouped under **Assets (.gaf)**, **Tilesheets (.gts)**,
**Animations (.gani)**, **Scenes (.gscn)**, **Sprites (.gspr)**, **Sound (.gsnd)**,
and **Misc** in each directory. Each type has a distinct placeholder icon;
ZIP asset containers share the Assets group. Empty groups remain visible.
Entries inside each `.gaf` in the displayed directory are loaded in the background
and appended after loose files, grouped by stored asset type (the five native
definition types use their existing groups; other types use Misc). Labels retain
the full internal entry name followed by `(archive.gaf)`, and tooltips show the
absolute archive path, entry name, and type. Identical loose and packed names,
including entries from different archives, remain separate.

Double-click a native packed definition, or choose **Open**, to edit it in a normal
document tab. All five formats are supported, including multi-definition entries.
Packed binary entries have no Open command, and Open External is unavailable for
all packed entries. Loose and packed GTS/GANI/GSCN/GSPR documents can preview their explicit
packed dependencies through the [shared authoring resolver](../Gondwana.Tooling.Assets/README.md)
without loose copies or reference rewriting. Refresh rereads archive contents;
subdirectories are scanned only when expanded. Unreadable, corrupt, or protected
archives are logged and skipped without password prompts. Explicitly opening an
encrypted GAF asks for its password; after authentication, refresh can list its
entries using session credentials without prompting again. No files are extracted
or changed. Scans use detached `AssetsFile` instances, disposed after each archive;
the existing loader buffers one archive's payloads at a time, so large archives
may take time to appear. Navigating or refreshing discards stale scan results.
Studio GAF saves cancel obsolete scans and asynchronously wait for the active
archive reader to release its handle before writing. The affected document is
temporarily disabled, duplicate save requests share the pending save, and
save-on-close/shutdown waits for success. The UI continues processing messages;
no synchronous wait or change to GAF serialization is involved. Browsing refreshes
after the write attempt.

## Editing packed definitions

Tabs identify packed entries as `entry.gts (archive.gaf)`; tooltips include the full
archive path, stored asset type, and exact entry name. Identity uses `AssetLocation`,
not the caption. Reopening the same typed entry activates its existing tab, while
loose duplicates and entries from different archives remain separate documents.
The existing editor models load all members through the native serializers and
retain their physical packed origin separately from logical definition identities.

**Save** commits and validates the complete document, then replaces just its typed
entry in a freshly loaded archive. Studio stages and validates a sibling GAF,
flushes it, and installs it by atomic file replacement. Unrelated entry payloads,
encryption, and dependency references are preserved. Failed validation, conflict,
or persistence leaves the original archive and dirty document intact. Existing
editor rules still allow explicitly saving incomplete drafts; duplicate logical
identities remain invalid.

Studio serializes GAF and packed-document saves through the directory browser's
existing asynchronous scan/save gate. Whole-archive fingerprints detect external
changes; per-entry baselines detect conflicting changes by another Studio editor.
Sequential saves to different entries preserve each other's changes. A dirty GAF
editor must be saved before a child can save. A clean parent refreshes after a
child saves; unsaved child edits are never replaced. If the parent changes that
child's entry, the child's save is rejected. Use **File > Reload document from
disk** to accept the saved contents (with confirmation before discarding edits),
or **Save As** to preserve the current edits separately.

For a packed definition, **Save As** writes a loose file and adopts it as the tab's
new editing location. The original GAF entry is unchanged. Relative dependencies
are rebased from the archive directory to the final loose-file directory, including
images that implicitly used their containing archive. Save As into another GAF
entry is not offered.

Successful saves invalidate the shared package catalog. Editors using that archive
show `[reload]`; **File > Reload dependency previews** refreshes previews without
discarding edits. A changed document entry requires **Reload document from disk**;
reloading previews cannot clear that conflict indication. The separate Scene Viewer
still accepts loose scene files only; packed scenes use Studio's built-in preview,
or can be adopted as loose files with Save As before launching that viewer.

Right-click a file for **Open** in the built-in editor (except Misc), or
**Open External** to choose an application using Windows' Open with dialog
(except `.gaf`). External opening uses the saved file on disk.
Double-click supported files or use **File > Open…**; opening a path already
in the workspace activates its existing document. Selecting a document tab also
selects and reveals its corresponding entry in **Working directory**, expanding
only necessary folders. This works for loose files and typed packed GAF entries,
including entries that finish loading after the tab becomes active. Refresh and
Save As update the highlight; untitled documents and files outside the working
directory leave nothing selected. Switching tabs does not move keyboard focus
from the editor. **File > New** offers all six formats. Right-click a type group
in **Working directory** (including a nested folder) to use **Add New Sprite**,
**Add New Scene**, etc. These invoke the same new-document workflow; Save defaults
to the chosen folder. GAF creation asks for a destination and optional password
protection. Right-click a loose file or packed entry for **Show Dependencies** or
**Show Dependents** to inspect that exact resource in Dependency Explorer.

**Save**, **Save As**, and **Close** operate on the active outer document. A `*`
marks unsaved changes. Save As adopts the destination and updates open-document
tracking; dependency rebasing remains the responsibility of each format's own
document model. Validation errors require confirmation before saving. Closing a
dirty document or exiting offers Save / Don't Save / Cancel.

## Hosting architecture

`MainForm` owns the outer DockPanelSuite workspace, Working directory, Output,
file commands, document tracking, and plugin contributions. Each
`StudioDockDocument` contains exactly one reusable `UserControl`:

| Format | Hosted control | Authoritative document |
| --- | --- | --- |
| GAF / ZIP | `AssetEditorControl` | `AssetsFile` |
| GTS | `TilesheetEditorControl` | Tooling `TilesheetDocument` |
| GANI | `AnimationEditorControl` | `AnimationDocument` |
| GSND | `AudioEditorControl` | `AudioDocument` |
| GSCN | `SceneEditorControl` | `SceneDocument` |
| GSPR | `SpriteEditorControl` | `SpriteDocument` |

The internal `StudioDocument` adapter binds shell operations to those existing
APIs. It owns no alternate format model, serializer, dependency resolver, or
editor implementation. Studio directly references the six tooling projects.
The existing project path and `Gondwana.Studio.WinForms` assembly name remain.

Each editor owns its **inner** docking surface. Its panes stay inside that
editor; they are not global Studio tools. **View** restores global tools and
uses the active editor's `PaneNames`, `IsPaneVisible`, `ShowPane`, and
`ShowAllPanes` APIs to recover hidden panes. Restoration reuses the existing
contents and arrangement. Newly opened documents use the last saved layout for their editor type.

On close, Studio detaches document change handlers and disposes the editor,
including its inner dock contents and preview resources. Asset documents and
packed-image previews use detached `AssetsFile` instances, so opening authoring
files does not register runtime asset packages. Studio owns its shared packed
resource catalog and disposes it at shutdown. The GAF editor owns replacement
packages adopted through `AcceptSavedArchive`; Studio retains ownership of
the originally supplied package.

## Plugins and scope

`Studio.Core` retains shell/plugin infrastructure and Output state. Plugins in
the application's `plugins/` directory can implement the Core `IStudioPlugin`
contract and its WinForms extension to contribute global panels and menu items.
The working-directory lifecycle invokes the existing project-open/close hooks;
this does not imply an EngineState project format.

[Project Diagnostics](../Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics/README.md)
is the first working plugin and the reference for writing another one. It adds a
dockable authoring-file/reference report and **Plugins > Project Diagnostics >
Rescan**. Building its project or the solution automatically deploys its DLL
under Studio's output `plugins/` directory. Build that project before the run
command above to include it. Studio works without it; all six editors remain
built in. The loader shares Core and WinForms contracts with the host to preserve
interface identity across plugin load contexts. See the plugin README for
lifecycle, deployment, dependency rules, and a minimal plugin example.

The deprecated Studio Avalonia prototype and duplicate Studio format editors
have been retired. The runtime Avalonia adapters and game templates are
unaffected. EngineState project composition is a separate follow-up. Studio
does not replace the standalone utilities or introduce Studio-owned file formats.

## Validation

The optional **External Asset Importer** plugin converts supported Godot 3.x and 4.x
TileSets, Tiled TSX/TMX content and Aseprite sprites into native authoring files.
Build `Tooling/Gondwana.Tooling.Studio.Plugin.ExternalImport` with Studio's matching
Debug/Release configuration, then restart Studio. Its build deploys the plugin and
headless importer dependency into Studio's `plugins/` directory. See the
[plugin README](../Gondwana.Tooling.Studio.Plugin.ExternalImport/README.md) for
deployment, lifecycle, analysis/import workflow and supported-format limits.

`Testing/Gondwana.Tooling.Studio.WinForms.Tests` exercises actual controls on STA
threads: outer/inner ownership, all format adapters, dirty state, saving and
path adoption, duplicate activation, pane recovery, cancellation, encryption,
and disposal. Run it on Windows alongside the standalone interaction suites
and the baseline `Gondwana.Tests` suite.

## Dock layout preferences

DockPanelSuite layouts are saved per user under
`%LOCALAPPDATA%/Hidden Worlds Games/Gondwana/Tooling/<entry-application>/Docking/`.
Outer tool windows and each editor type (GAF, GTS, GANI, GSND, GSCN, GSPR) have separate
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

## Acknowledgments

Gondwana Studio uses [DockPanel Suite](https://github.com/dockpanelsuite/dockpanelsuite),
the Visual Studio-inspired docking library for .NET WinForms originally created by
**Weifen Luo** and developed further by its open-source contributors. Many thanks
to Weifen Luo and the DockPanel Suite contributors for making the library
available to the .NET community.

## Workspace session restoration

Studio and the six standalone editors remember their own working directory,
open saved document paths, tab order, and active document. Each application stores
`workspace-session.json` under its existing per-user preference directory, beside
`Docking/`. Studio uses its previous project-folder preference as a fallback when
no session file exists. Dock layouts remain independent.

Use **File > Restore workspace on startup** to turn restoration off. Use
**File > Start empty on next launch**, hold **Shift** while launching, or pass
`--empty-workspace` to bypass restoration. The menu's one-time request is consumed
at the next startup. Preference-write failures are reported in Studio Output or
a standalone warning dialog, including after startup and during shutdown. Failed
option writes leave the previous setting selected. For example:

```console
dotnet run --project Tooling/Gondwana.Tooling.Studio.WinForms -- --empty-workspace
```

Documents reopen through normal editor loaders using their current disk contents.
Missing, unreadable, corrupt, or unsupported files are skipped; Studio reports the
reason in Output and standalones show one summary. Encrypted archives request their
password again; canceling skips that archive and continues. Restoring never creates
missing archives or writes source content. A disabled plugin does not prevent the
built-in editors from reopening.

Only an approved shutdown captures a session, before documents are disposed and
after save prompts have completed. Canceling exit preserves the previous session;
Save/Save As adopts the final saved path. Untitled documents and unsaved edits are
not recovered. Session metadata contains no document contents, passwords, archive
payloads, runtime EngineState, or editor-local selection/camera state. Packed
documents record archive path, asset type, and entry name, including the active
packed tab. Older loose-only session preferences remain compatible. Missing or
renamed entries are skipped with a diagnostic while unrelated documents restore.

Passwords verified while loading dependency previews are also reused for document
opens and preview reloads during the session. During startup restoration, a failed
or canceled archive authentication skips the remaining documents from that archive
without repeating the prompt. Other archives and loose documents still restore;
explicitly opening the failed archive afterward allows another attempt.

## Appearance

Choose **View → Theme → Dark, Dark Blue, or Light**. The choice is shared across Studio and all six standalone editors and takes effect on the next launch. Open documents remain untouched; restart normally after saving your work. See [shared tooling themes](../Shared/WinForms/Theming/README.md) for persistence, control coverage, and DockPanel Suite limitations.
## Dependency Explorer

Use **File > Find References**, **Show Dependencies**, or **Show Dependents**, or
the equivalent document-tab and working-directory context menus. **View >
Dependency Explorer** restores the dockable pane. Double-click a result or choose
**Open Referenced Definition** to activate its editor and select its logical
member. Inspection alone does not navigate. The **Selected Member / Whole
Document** query selector switches between one logical definition and every
member in its physical document, using the same completed scan. The selected
member remains available when switching back; when inspection starts from a
working-directory file or packed entry without a member, only **Whole Document**
is available until a member-scoped query is started from an editor using **Find
References**, **Show Dependencies**, or **Show Dependents**. **Refresh / Rescan**
rebuilds the saved-content snapshot, independently of query scope.

Results show full physical paths, typed GAF entry names (including nested paths),
logical identities, and reference field paths. Already-open editors are activated
without reload, including dirty editors. Unsupported field selection falls back
to the logical member and reports the exact reference path. Non-definition media
and remote URIs are displayed but do not have native definition editors.

The scope is the current working directory, with the shared scanner's exclusions
and link safety checks. Scanning runs off the UI thread and completed results are
reused across selections. Saves, reviewed asset operations, and workspace changes
invalidate results; use Refresh / Rescan afterward. Obsolete scans are cancelled
and cannot replace newer results. There is no filesystem watcher.

Discovery uses saved native GTS/GANI/GSCN/GSPR/GSND content, not unsaved editor
models or arbitrary C# code. The pane warns when editors have unsaved changes.
Failures are shown as incomplete results with missing, ambiguous, inaccessible,
invalid, or authentication diagnostics. A failed member may have undiscovered
references; an empty partial result does not establish that there are no users.
Open an encrypted archive with its correct password, then rescan. Targets outside
scope are identified explicitly; inbound results cover only inspected sources.
Physical references without selectors are never presented as exact member matches.

Core navigation is independent of optional plugins. Project Diagnostics retains
its separate validation scanner; the explorer uses `DefinitionReferenceDiscovery`
and its semantic handlers rather than repeating that plugin's parser. Shared
`ResourceAddress`, `DefinitionReferenceQuery`, and `IResourceNavigator` contracts
live in `Gondwana.Tooling.Assets` and have no WinForms dependency. They can also
serve later authoring workflows; this feature does not implement drag-and-drop.
