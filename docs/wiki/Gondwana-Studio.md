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

- [[Studio & Desktop Tools]]
- [[Standalone Desktop Tools]]
- [[External Asset Importing]]
- [[.gscn Files|GSCN-Files]]
- [[Gondwana CLI Cheatsheet]]
