# Gondwana Studio

Gondwana Studio is the Windows authoring shell for `.gaf` / `.zip` asset
containers, `.gts` tilesheets, `.gani` animations, `.gsnd` sounds, and `.gscn`
scenes. It hosts the same public editor controls as the standalone utilities.
It does not require a GameHost or a running game loop.

## Running

```console
dotnet run --project Tooling/Gondwana.Tooling.Studio.WinForms --configuration Release
```

Choose **File > Open working directory…** to browse authoring files. Directories
expand lazily. Double-click files or use **File > Open…**; opening a path already
in the workspace activates its existing document. **File > New** offers all five
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

The internal `StudioDocument` adapter binds shell operations to those existing
APIs. It owns no alternate format model, serializer, dependency resolver, or
editor implementation. Studio directly references the five tooling projects.
The existing project path and `Gondwana.Studio.WinForms` assembly name remain.

Each editor owns its **inner** docking surface. Its panes stay inside that
editor; they are not global Studio tools. **View** restores global tools and
uses the active editor's `PaneNames`, `IsPaneVisible`, `ShowPane`, and
`ShowAllPanes` APIs to recover hidden panes. Restoration reuses the existing
contents and arrangement. Newly opened documents use default layouts; layout
persistence is not implemented.

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

The deprecated Studio Avalonia prototype and duplicate Studio format editors
have been retired. The runtime Avalonia adapters and game templates are
unaffected. EngineState project composition is a separate follow-up. Studio
does not replace the standalone utilities or introduce Studio-owned file formats.

## Validation

`Testing/Gondwana.Tooling.Studio.WinForms.Tests` exercises actual controls on STA
threads: outer/inner ownership, all format adapters, dirty state, saving and
path adoption, duplicate activation, pane recovery, cancellation, encryption,
and disposal. Run it on Windows alongside the standalone interaction suites
and the baseline `Gondwana.Tests` suite.
