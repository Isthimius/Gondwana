# Gondwana Studio

Gondwana Studio is the integrated Windows authoring shell for Gondwana's native content formats. It hosts the same public editor controls used by the standalone WinForms tools.

Studio is optional development tooling. It does not replace Gondwana's code-first runtime model, and games do not depend on Studio.

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

**File > New** supports all six built-in formats.

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

Layout state includes docking positions, tab groups, splits, relative sizes, and hidden-pane visibility. It does not modify Gondwana content files and does not save document data or automatically reopen documents.

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
