# Gondwana Studio

Gondwana Studio is the Windows authoring shell for `.gaf` / `.zip` asset
containers, `.gts` tilesheets, `.gani` animations, `.gsnd` sounds, `.gspr` sprite collections, and `.gscn`
scenes. It hosts the same public editor controls as the standalone utilities.
It does not require a GameHost or a running game loop.

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

Packed entries are browsing-only: they have no Open or Open External command and
double-click does not open them. Packed previews/resolution and document editing
are deferred to subsequent integration steps. Refresh rereads archive contents;
subdirectories are scanned only when expanded. Unreadable, corrupt, or protected
archives are logged and skipped without password prompts. No files are extracted
or changed. Scans use detached `AssetsFile` instances, disposed after each archive;
the existing loader buffers one archive's payloads at a time, so large archives
may take time to appear. Navigating or refreshing discards stale scan results.
Studio GAF saves cancel obsolete scans and asynchronously wait for the active
archive reader to release its handle before writing. The affected document is
temporarily disabled, duplicate save requests share the pending save, and
save-on-close/shutdown waits for success. The UI continues processing messages;
no synchronous wait or change to GAF serialization is involved. Browsing refreshes
after the write attempt.

Right-click a file for **Open** in the built-in editor (except Misc), or
**Open External** to choose an application using Windows' Open with dialog
(except `.gaf`). External opening uses the saved file on disk.
Double-click supported files or use **File > Open…**; opening a path already
in the workspace activates its existing document. **File > New** offers all six
formats. GAF creation asks for a destination and optional password protection.

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
image catalog and disposes it at shutdown. The GAF editor owns replacement
packages created by its path-adopting `SaveTo` API; Studio retains ownership of
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
no source-document paths or unsaved content are stored, and documents are never reopened
on startup. Missing or corrupt preferences fall back to defaults without a dialog.
Available plugin tools in Studio use stable plugin identities; missing plugins are ignored.
Changes are saved after a short settling interval and flushed on disposal.

## Acknowledgments

Gondwana Studio uses [DockPanel Suite](https://github.com/dockpanelsuite/dockpanelsuite),
the Visual Studio-inspired docking library for .NET WinForms originally created by
**Weifen Luo** and developed further by its open-source contributors. Many thanks
to Weifen Luo and the DockPanel Suite contributors for making the library
available to the .NET community.
