# Gondwana Studio

Gondwana Studio is the integrated Windows authoring shell for Gondwana's native content formats. It hosts the same public editor controls used by the standalone WinForms tools.

Studio is optional development tooling. It does not replace Gondwana's code-first runtime model, and games do not depend on Studio.

## Undo and redo

**Edit > Undo** / **Ctrl+Z** and **Edit > Redo** / **Ctrl+Y** operate on the
active GAF/ZIP, GTS, GSCN, GANI, GSPR, or GSND document in both Studio and the standalone editors.
Ctrl+Shift+Z also redoes. Textboxes and property-grid fields with pending text edits retain local
text undo. Commit the value before using keyboard document history; menu commands
commit valid pending property edits first. Non-editing grid values, trees, lists,
canvases, and drop-down-only selectors route to the document.
Selection, zoom, and preview-only visibility are not document edits.

Undoing back to the last successful save clears the dirty marker; undoing past
that save makes it dirty again. Save and Save As retain history and establish a
checkpoint only after success. New unsaved documents remain dirty. Editing after
Undo discards the redo branch. Each document retains up to 200 committed operations
by default; evicting history never makes an unreachable saved state clean.

History includes in-memory edits to loose and GAF-backed GTS/GSCN/GANI/GSPR/GSND definitions.
It cannot reverse committed archive relocation, file deletion, or cross-file
reference rewrites. GAF Add/Import (including dependencies) is one step per approved
import; Replace retains the exact typed entry and payload. Identical replacements
and zero-addition imports create no step. Scene painting and fill tools are not included. Audio history changes
GSND metadata; it never deletes media, downloads URIs, or rewrites an audio archive.

GAF histories retain only affected entry bytes, capped at 64 MiB across Undo and
Redo as well as 200 operations. Oldest applied entries, then furthest redo entries,
are pruned. An edit larger than the budget prompts before committing: approval
performs it without Undo and clears earlier history only after success. Cancel or
ordinary failure preserves earlier history. No plaintext history spill files are used.
Closing, pruning, or abandoning redo releases retained payload references.

Studio GAF Save As adopts the new path and establishes a checkpoint; retained
native payload references rebase from their capture origin during replay. Undo
changes the adopted archive. Standalone GAF Save As creates a separate copy, keeps
the source active, and does **not** advance its checkpoint. Normal Save does.
Failed persistence leaves checkpoints unchanged. Search retains native text Undo.

Successful reviewed Rename/Move/Delete commits are **not locally undoable** and
reset the refreshed archive to a clean baseline. Failed or cancelled transactions
retain history. Callers mutating a borrowed `AssetsFile` must call `MarkChanged()`,
which discards stale history and marks the archive dirty. A packed-child save is
an external refresh for a clean parent GAF, so its obsolete history is cleared;
the child keeps its own independent history. Pending archive saves block replay.

Grouped model edits form one undo step. An empty group creates no step;
cancelling a group restores its original state. Scene tile commands preserve
sparse cells and can be composed into future painting transactions without
adding painting tools now. Save As keeps reference targets meaningful when
older source-reference edits are undone.


## Supported content

Studio currently hosts six built-in editors:

| Format | Hosted editor control |
| --- | --- |
| GAF / ZIP | `AssetEditorControl` |
| GTS | `TilesheetEditorControl` |
| GANI | `AnimationEditorControl` |
| GSND | `AudioEditorControl` |
| GSCN | `SceneEditorControl` |
| GSPR | `SpriteEditorControl` |

The editor controls and their document models remain authoritative. Studio's document adapter routes shell commands into those existing APIs; it does not maintain duplicate format models or serializers.

For the format semantics themselves, see [Authoring formats](Studio-and-Desktop-Tools#authoring-formats).

## Running Studio

From the repository root on Windows:

```console
dotnet run --project Tooling/Gondwana.Tooling.Studio.WinForms --configuration Release
```

Choose **File > Open working directory…** to select an authoring directory. Directories are expanded lazily in the working-directory browser.

Double-click a supported file, or use **File > Open…**, to open it as a Studio document. Opening a path that is already open activates the existing document.

**File > New** supports all six built-in formats. Right-click a Working Directory
type group (Assets, Tilesheets, Animations, Scenes, Sprites, or Sound) and choose
**Add New [type]** to start the same workflow. For nested folders, the Save dialog
defaults to that folder; new documents are not written until saved. The Misc group
has no creation command. On individual loose files and packed entries,
**Show Dependencies** and **Show Dependents** use the same Dependency Explorer
commands as the File menu.

## Documents and saving

Studio treats each hosted editor as one outer document.

The shell routes:

- Save;
- Save As;
- Close;
- dirty-state indicators;
- duplicate-document activation;
- close/exit prompts

through the active editor's existing authoring API.

A `*` marks a document with unsaved changes. Closing a dirty document or exiting Studio offers the normal Save / Don't Save / Cancel choice.

Each format remains responsible for its own dependency rebasing and validation semantics. Studio does not reinterpret those rules.

## Packed definition documents

The Working Directory lists entries from each `.gaf` after loose files in their
stored asset-type group. Internal entry paths and an `(archive.gaf)` suffix keep
duplicates distinguishable; tooltips show the full archive path, type, and entry.
Subdirectories load lazily. Passive scans skip inaccessible archives without
requesting passwords; explicitly opening an encrypted GAF authenticates it for
the session so a refresh can reveal its entries.

Double-click or choose **Open** for a packed GTS, GANI, GSCN, GSPR, or GSND to edit
it in its normal editor. Reopening the same typed entry activates its existing
tab. Loose duplicates and identical entries in other archives remain independent.
Unsupported binary entries have no Open command; packed entries cannot use Open
External. No extraction is required.

**Save** validates and serializes the complete document, including every logical
member, and replaces only that typed entry. Studio stages and validates the GAF
before atomic replacement, preserving unrelated entries and encryption. Save
failures preserve dirty edits and the original archive. Saves coordinate with
background scans without blocking the UI while waiting for archive access.

Sequential saves to different entries preserve each other's changes. A dirty
parent GAF must be saved before a child definition can save. Conflicting changes
to the same entry or external archive changes reject the save. **Reload document
from disk** requires confirmation before discarding edits; **Save As** can retain
the edits separately. For packed native definitions, Save As adopts a loose file
as the editing location, rebases outbound paths, and leaves the original entry
unchanged. Saving directly to another typed GAF entry is not offered.

Authoring previews honor explicit packed references even when loose duplicates
exist. GTS/GANI/GSCN/GSPR previews resolve nested definitions and images without
running the engine or changing dependency provenance. A saved dependency marks
affected open documents `[reload]`; **Reload dependency previews** refreshes them
without discarding unsaved edits. A changed document entry requires a full
document reload instead.

**View Scene** can launch a packed GSCN without Save As: Studio writes a uniquely named temporary loose snapshot with rebased references, and the Viewer uses the original content directory to discover neighboring assets and sprites. The snapshot includes unsaved edits, leaves the packed document and dirty state unchanged, and is deleted by the Viewer on normal exit. The runtime Viewer retains its existing loose-over-packed content-root precedence; Studio's authoring previews use explicit-source resolution.

## Dependency Explorer

Use **File > Find References**, **Show Dependencies**, or **Show Dependents** for
the selected logical definition. The same actions are available on document tabs
and working-directory file/packed-entry context menus. **View > Dependency
Explorer** restores the dockable pane. Its **Selected Member / Whole Document**
selector switches between the selected logical definition and all members of
its physical document, without rescanning. Switching back restores the current
member selection; when opening a query directly from a file/packed entry with
no member selected, **Selected Member** is disabled. **Refresh / Rescan**
rebuilds the saved-content snapshot and is independent of this query selector.
Inspecting results does not change the active editor; double-click a result or
choose **Open Referenced Definition** to navigate.

Dependencies and dependents retain the full physical path, expected asset type,
exact nested GAF entry name, logical member, and available reference field path.
Identically named entries in different archives remain distinct. Navigation
activates an existing editor or uses the normal editor factory and authentication
workflow. It never reloads or saves a dirty editor. When a field cannot be selected,
Studio selects the nearest supported member and displays the reference path.

The scan covers saved GTS, GANI, GSCN, GSPR, and GSND content in the current working
directory, including packed definitions. It reuses the asset tooling's discovery
rules, exclusions, and safety checks. Background scans are cancelled on workspace
changes or shutdown; stale results cannot overwrite a newer scan. Completed
results are reused until invalidated by saves or reviewed asset operations.
Use **Refresh / Rescan** after these operations or external changes.

Results describe saved content; a warning identifies unsaved editor changes that
are not represented. Missing, ambiguous, invalid, inaccessible, and encrypted
resources produce diagnostics and an incomplete-scan warning. A failure may
prevent inspection of remaining references in that member or scope. Empty partial
results are not evidence that a resource is unused. Open encrypted archives with
their correct passwords, then rescan. Outside-scope targets are identified, and
inbound queries cover only inspected sources. References without a logical selector
are grouped separately rather than attributed to a guessed member. Remote audio
URIs are displayed without fetching or opening them as local documents. Arbitrary
C# references are not discovered.

The pane is built into Studio and does not require Project Diagnostics. That
optional plugin continues to report validation problems; Dependency Explorer
focuses on relationships and navigation. The shared identity, query, and navigation
contracts are independent of the pane. No drag-and-drop or cross-document editing
is included.

## Docking model

Studio uses a two-level docking model:

```text
Studio outer workspace
    |
    +-- Working directory
    +-- Output
    +-- plugin panes
    +-- editor document
           |
           +-- editor-owned inner docking workspace
```

The outer shell owns global tools and documents. Each editor owns its own panes.

For example, the Scene editor owns panes such as Scene structure, Scene preview, source browsers, Properties, Tile properties, and Validation. Those panes stay inside the Scene document instead of becoming global Studio windows.

The **View** menu can restore global tools and hidden panes belonging to the active editor.

## Layout persistence

DockPanelSuite layouts are stored per user under:

```text
%LOCALAPPDATA%/Hidden Worlds Games/Gondwana/Tooling/<entry-application>/Docking/
```

Studio keeps separate profiles for the outer shell and for each editor type.

Layout state includes docking positions, tab groups, splits, relative sizes, and hidden-pane visibility. It does not modify Gondwana content files or save document data. A separate workspace-session preference controls document reopening.

Useful recovery commands include:

- **View > Reset application layout**
- **View > Reset active editor layout**
- individual pane commands
- the active editor's **Show all … panes** command

Studio and the standalone applications keep independent layout profiles.

## Plugins

Studio can discover optional plugins from its `plugins/` directory at startup.

Plugins can contribute global panels and menu items and can receive working-directory lifecycle notifications. They do not replace Studio's six built-in editors.

### Project Diagnostics

The Project Diagnostics plugin performs read-only inspection of the active working directory. It discovers GAF, GTS, GANI, GSND, GSCN, and GSPR files and reports explicit-reference, validation, missing-file, malformed-path, and missing packed-entry problems.

Its role is cross-cutting project inspection rather than content editing.

### External Asset Importer

The External Asset Importer plugin converts supported foreign authoring formats into native Gondwana files. Current providers cover:

- Godot 3.x and 4.x TileSets;
- Tiled TSX/TMX;
- Aseprite ASE/ASEPRITE.

The importer analyzes before writing, reports diagnostics, stages output, and validates native definitions. See [[External Asset Importing]] for the detailed support matrix.

## Scene Viewer integration

The reusable GSCN editor includes **View Scene**, so the command is available in Studio as well as the standalone Scene editor.

It launches `Gondwana.Tooling.SceneViewer.WinForms` as a separate process after ensuring the document is saved. The viewer then loads the saved GSCN into the real Gondwana runtime.

This is intentionally separate from the editable Scene Preview:

| Scene Preview | Scene Viewer |
| --- | --- |
| Definition-driven | Runtime-driven |
| Embedded in the editor | Separate process |
| Editable | Read-only |
| Representative/static animation preview | Real GANI playback |
| No GameHost | Uses the WinForms GPU host |

See [[.gscn Files|GSCN-Files]] for scene serialization and runtime-viewer details.

## What Studio is not

Studio is not:

- required to create or run a Gondwana game;
- a replacement for the standalone editors;
- a second set of content formats;
- an EngineState project composer;
- a running gameplay environment;
- the owner of foreign-format import logic.

The deprecated Avalonia Studio prototype and its duplicate editor stack have been retired. Current Studio authoring is WinForms-based; this does not affect Gondwana's Avalonia runtime host or templates.

## Where to read next

- [[Studio & Desktop Tools|Studio-and-Desktop-Tools]]
- [[Standalone Desktop Tools]]
- [[External Asset Importing]]
- [[.gscn Files|GSCN-Files]]
- [[Gondwana CLI Cheatsheet]]

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

## Reference-aware content moves

Use **File → Move / rename content…** for closed loose files and whole GAFs. Use the hosted GAF editor’s **Rename / Move…** for packed entries. Studio suggests its working directory as the discovery scope, but you must review it. Save/close other authoring documents first. The shared transaction updates only inspected Gondwana references, refreshes the workspace and package catalog, and preserves logical member identities. Reopen moved documents at their new paths. Save As remains a copy, with portable outbound references and no inbound updates.

See [Assets Files — reference-aware moves](https://github.com/Isthimius/Gondwana/wiki/Assets-Files#reference-aware-rename-and-move) for all storage transitions, scope boundaries, encryption, rollback and interrupted-commit recovery. Outside-scope references and arbitrary C# are not rewritten. Existing pack/unpack semantics are unchanged.

## Reference-aware deletion

Use the shared GAF editor’s **Delete** for typed entries, or **File → Delete content…** for a batch of closed loose files and whole GAF/ZIP archives. Review the suggested working-directory scope and inbound references. Safe deletion blocks surviving references; **Delete anyway…** requires separate authorization to leave them dangling without deleting dependents. Save the current GAF and close other authoring models; whole-archive deletion requires closing that archive too. Successful operations commit to disk, refresh the workspace and package catalog, and preserve the borrowed editor instance for entry deletions.

See [Assets Files — reference-aware deletion](https://github.com/Isthimius/Gondwana/wiki/Assets-Files#reference-aware-deletion) for scope, credentials, batching, force semantics, and recovery guarantees.

## Export and operation persistence

The hosted Assets editor's **Export** writes one complete entry as a loose copy using the same semantic planner as CLI unpack. Definitions preserve all members and rebase their outbound references; dependencies not exported remain packed. Save a dirty source archive first when exporting a native definition. Export does not change the archive, mark it dirty, or rewrite inbound references.

Add / Import and Replace are unsaved archive edits. Move/Delete are reviewed disk transactions and refresh the current archive/workspace after success. Save As is a copy with outbound rebasing; Studio adopts the new path through its normal Save routing and retains the original file. Replace remains a same-identity byte operation without dependency repair.

See [Reference Relocation](Reference-Relocation#shared-tooling-and-safety) for shared architecture, scan scope, credentials, recovery, and deferred capabilities; [Assets Files](Assets-Files) owns the overall workflow model.

## Workspace sessions

Studio restores its last working directory, open saved files, tab order, and active
document. The standalone editors maintain independent sessions. Session metadata
is stored per application in `workspace-session.json` beside the `Docking` folder.
Existing Studio project-folder preferences serve as a fallback before the first
session is saved.

Use **File > Restore workspace on startup** to opt out. **File > Start empty on
next launch**, holding Shift while launching, or passing `--empty-workspace`
starts without restoration. The menu option applies to one launch.

Files reopen through normal editor loaders using current disk contents. Missing,
corrupt, or unsupported files are skipped; see Studio's Output or the standalone
startup summary for the reason and reopen the corrected file with File > Open.
Encrypted archives request their passwords again; canceling skips that archive.
Invalid session metadata cannot prevent startup.

Sessions are captured after successful save/close approval and before disposal.
Canceling exit preserves the prior session. Unsaved edits, untitled documents,
passwords, archive bytes, EngineState, and editor-local selection/view state are
not persisted. Packed documents restore using archive path, stored asset type,
and exact entry name, including the active packed tab. Missing entries are skipped
with diagnostics while unrelated documents continue restoring. Existing loose-only
workspace preferences remain compatible.
