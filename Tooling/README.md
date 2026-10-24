# Gondwana Tooling

This directory contains Gondwana's content-authoring applications, the Studio
shell, runtime scene previewer, asset-management infrastructure, developer
utilities, plugins, and repository maintenance scripts.

**Gondwana is code-first:** games do not need Studio or any of these authoring
applications to run. Native definition formats are owned by the engine; tools
edit those formats rather than introducing Studio-specific equivalents.

## Where to start

| Goal | Start with |
| --- | --- |
| Edit multiple Gondwana asset types in one application | [Gondwana Studio](./Gondwana.Tooling.Studio.WinForms/README.md) |
| Edit one asset type without launching Studio | [Standalone content editors](#standalone-content-editors) |
| See how a saved scene actually renders in the engine | [Scene Viewer](./Gondwana.Tooling.SceneViewer.WinForms/README.md) |
| Pack, inspect, publish, or deploy from the command line | [Gondwana CLI](./Gondwana.Cli/README.md) |
| Create a new game project | [Project templates](./Gondwana.Templates/README.md) |

## Downloadable Windows tools

Tagged GitHub Releases include `Gondwana-<version>-binaries.zip`. Extract the **entire**
archive, keeping the application directories under `Tooling/` together:

- `Gondwana.Tooling.Studio.WinForms/Gondwana.Studio.WinForms.exe` — integrated Studio,
  including the official Project Diagnostics and External Asset Importer plugins under `plugins/`.
- `Gondwana.Tooling.{Assets,Animations,Audio,Scenes,Sprites,Tilesheets}.WinForms/` —
  standalone editors, each with its own executable and runtime dependencies.
- `Gondwana.Tooling.SceneViewer.WinForms/` — GPU-backed scene viewer shared by
  Studio and the standalone Scene editor.

The Windows x64 applications are published **self-contained**, so they do not
require a separately installed .NET Desktop Runtime. They are portable folder
distributions, not single-file executables or installers. Keep the complete
contents of each application folder. In particular, do not move the Scene Viewer
away from its sibling folder: **View Scene** locates it relative to the
application's directory.

The release and CI workflows use the shared
[`Publish-WindowsTooling.ps1`](./scripts/Publish-WindowsTooling.ps1) publisher and
[`Test-WindowsToolingBundle.ps1`](./scripts/Test-WindowsToolingBundle.ps1) bundle
validator to keep the layout consistent.

## Content authoring

### Gondwana Studio

[**Gondwana.Tooling.Studio.WinForms**](./Gondwana.Tooling.Studio.WinForms/README.md)
is the Windows authoring shell. It opens GAF/ZIP, GTS, GANI, GSND, GSCN, and
GSPR documents side by side, with a working-directory browser, Output,
document commands, and optional plugins.

Studio **hosts the same six editor controls** used by the standalone WinForms
applications. It does not maintain a separate implementation of each editor or
require an Engine, GameHost, or Studio-specific project format.

### Standalone content editors

Each application below can run independently; its reusable primary editor
control is also hosted in Studio.

| Format | Standalone editor | Purpose |
| --- | --- | --- |
| GAF / ZIP | [Assets](./Gondwana.Tooling.Assets.WinForms/README.md) | Package management; semantic import/export, reference-aware move/delete, entries, and encryption |
| GTS | [Tilesheets](./Gondwana.Tooling.Tilesheets.WinForms/README.md) | Image-backed tilesheets, frames, regions, and collision metadata |
| GANI | [Animations](./Gondwana.Tooling.Animations.WinForms/README.md) | Animation cycles, frame sequences, GTS dependencies, and preview |
| GSCN | [Scenes](./Gondwana.Tooling.Scenes.WinForms/README.md) | Scenes, layers, sparse tile placements, GTS/GANI sources, and authoring preview |
| GSPR | [Sprites](./Gondwana.Tooling.Sprites.WinForms/README.md) | Sprite collections, scene/layer and frame assignments, properties, and preview |
| GSND | [Audio](./Gondwana.Tooling.Audio.WinForms/README.md) | Sound definitions, audio source references, and playback settings |

All five native definition formats (GTS, GANI, GSCN, GSPR, and GSND) support
multi-definition documents. For editor capabilities and known preview limitations,
follow the editor links above.

### Runtime scene preview

[**Gondwana.Tooling.SceneViewer.WinForms**](./Gondwana.Tooling.SceneViewer.WinForms/README.md)
loads a saved GSCN into a real GPU-backed Gondwana Engine host. It is separate
from the GSCN editor's lightweight, definition-driven preview. Scene Viewer
provides camera movement and zoom, animation pause, and F3 runtime diagnostics;
it is a viewer, not a scene editor.

## Asset workflows and Studio plugins

### Shared asset-management implementation

[**Gondwana.Tooling.Assets**](./Gondwana.Tooling.Assets/) contains UI-independent
asset workflows shared by CLI and the Assets editor, including semantic
pack/unpack preparation, import, scoped reference discovery, relocation, and
deletion. Studio uses the hosted Assets editor rather than a parallel workflow.

See [Assets Files](../docs/wiki/Assets-Files.md) and
[Reference Relocation](../docs/wiki/Reference-Relocation.md) for the physical-file,
typed-entry, logical-member, and reference-safety contracts.

### External asset importing

[**Gondwana.Tooling.Importers**](./Gondwana.Tooling.Importers/) is the
UI-independent conversion pipeline for supported Godot, Tiled, and Aseprite
content.

[**External Asset Importer plugin**](./Gondwana.Tooling.Studio.Plugin.ExternalImport/README.md)
provides Studio's Analyze / Import interface, using that same pipeline to
produce native GTS, GANI, and GSCN content. See the
[format support matrix](../docs/wiki/External-Asset-Importing.md).

### Project diagnostics

[**Project Diagnostics plugin**](./Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics/README.md)
adds an optional, read-only Studio report of authored files, explicit
references, and validation or missing-resource problems. It is not the
runtime inspector or a replacement for the asset-operation dependency planner.

Plugins are optional. Studio's six built-in editors do not depend on them.
Follow each plugin's README for deployment and lifecycle requirements.

## Command-line tools and project setup

| Project | Purpose |
| --- | --- |
| [Gondwana.Cli](./Gondwana.Cli/README.md) | `gondwana` CLI: scaffolding, diagnostics, asset workflows, publish/deploy, and related tasks |
| [Gondwana.Templates](./Gondwana.Templates/README.md) | `dotnet new` templates for WinForms, Avalonia, and Blazor games |
| [Gondwana.Mcp](./Gondwana.Mcp/README.md) | Read-only MCP server exposing the official repository and wiki to compatible AI clients |

## Shared infrastructure and maintenance

| Project or directory | Purpose |
| --- | --- |
| [Gondwana.Tooling.Studio.Core](./Gondwana.Tooling.Studio.Core/) | UI-independent Studio plugin contracts and shell Output state |
| [Shared](./Shared/) | Source-linked reusable WinForms tooling infrastructure, including nested dock workspaces |
| [scripts](./scripts/README.md) | PowerShell helpers for setup, changelogs, publishing, deployment, releases, and maintenance |

## Workspace and dock-layout preferences

Studio and the standalone editors have independent per-user docking profiles.
Within each application, the outer tool-window arrangement and the six editor
types (GAF, GTS, GANI, GSND, GSCN, GSPR) retain their own layouts.

Profiles are stored under
`%LOCALAPPDATA%/Hidden Worlds Games/Gondwana/Tooling/<entry-application>/Docking/`.
Tabs, split proportions, and hidden-pane visibility persist across launches.
Use **View → Reset application layout**, **View → Reset active editor layout**,
or an editor's **Show all … panes** command to recover a workspace.

These preferences never alter Gondwana asset files and do not save source-document
paths or unsaved changes. **Open documents are not restored on startup.**
Missing or invalid preferences fall back to defaults. For detailed ownership,
pane recovery, and plugin behavior, see the
[Studio documentation](./Gondwana.Tooling.Studio.WinForms/README.md).
