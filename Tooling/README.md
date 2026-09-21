# Tooling

This folder contains Gondwana's developer tools, standalone content-authoring
utilities, the Studio shell, shared tooling UI infrastructure, and repository
automation scripts.

The standalone WinForms authoring tools are deliberately reusable: their primary
editor surfaces are hostable controls that can also be composed into Gondwana
Studio rather than reimplemented there.

## Content-authoring tools

- [`Gondwana.Tooling.Assets.WinForms`](./Gondwana.Tooling.Assets.WinForms/)  
  WinForms editor for Gondwana asset containers (`.gaf` / `.zip`), including
  import, export, replacement, renaming, filtering, and Save As.  
  See: [`Gondwana.Tooling.Assets.WinForms/README.md`](./Gondwana.Tooling.Assets.WinForms/README.md)

- [`Gondwana.Tooling.Tilesheets.WinForms`](./Gondwana.Tooling.Tilesheets.WinForms/)  
  WinForms editor for Gondwana `.gts` tilesheet definitions, regions, frames,
  collision metadata, and image-backed preview.  
  See: [`Gondwana.Tooling.Tilesheets.WinForms/README.md`](./Gondwana.Tooling.Tilesheets.WinForms/README.md)

- [`Gondwana.Tooling.Animations.WinForms`](./Gondwana.Tooling.Animations.WinForms/)  
  WinForms editor for Gondwana `.gani` animation definitions, with GTS-backed
  frame browsing, dependency metadata, and preview.  
  See: [`Gondwana.Tooling.Animations.WinForms/README.md`](./Gondwana.Tooling.Animations.WinForms/README.md)

- [`Gondwana.Tooling.Audio.WinForms`](./Gondwana.Tooling.Audio.WinForms/)  
  WinForms editor for Gondwana `.gsnd` sound definitions, including source
  metadata and portable playback settings.  
  See: [`Gondwana.Tooling.Audio.WinForms/README.md`](./Gondwana.Tooling.Audio.WinForms/README.md)

- [`Gondwana.Tooling.Scenes.WinForms`](./Gondwana.Tooling.Scenes.WinForms/)  
  WinForms editor for Gondwana `.gscn` scene definitions, including SceneLayer
  editing, GTS/GANI dependency browsing, sparse tile editing, and
  definition-driven preview.  
  See: [`Gondwana.Tooling.Scenes.WinForms/README.md`](./Gondwana.Tooling.Scenes.WinForms/README.md)

## Developer and integration tools

- [`Gondwana.Cli`](./Gondwana.Cli/)  
  The `gondwana` command-line tool for project scaffolding, health checks,
  package management, asset/tilesheet tasks, and publish/deploy workflows.  
  See: [`Gondwana.Cli/README.md`](./Gondwana.Cli/README.md)

- [`Gondwana.Mcp`](./Gondwana.Mcp/)  
  Read-only Model Context Protocol server that exposes the official Gondwana
  repository and wiki to MCP-capable AI clients.  
  See: [`Gondwana.Mcp/README.md`](./Gondwana.Mcp/README.md)

- [`Gondwana.Templates`](./Gondwana.Templates/)  
  `dotnet new` templates for creating Gondwana WinForms, Avalonia, and Blazor
  starter projects.  
  See: [`Gondwana.Templates/README.md`](./Gondwana.Templates/README.md)

## Gondwana Studio

Studio composes the five standalone authoring controls in one WinForms shell:

- [`Gondwana.Tooling.Studio.Core`](./Gondwana.Tooling.Studio.Core/)  
  UI-independent plugin infrastructure and shell Output state.

- [`Gondwana.Tooling.Studio.WinForms`](./Gondwana.Tooling.Studio.WinForms/)  
  Combined Windows authoring shell for GAF/ZIP, GTS, GANI, GSND, and GSCN.
  See its [embedding and architecture documentation](./Gondwana.Tooling.Studio.WinForms/README.md).

The newer standalone GAF/GTS/GANI/GSND/GSCN editors are the authoritative
format-specific authoring surfaces. Studio hosts them directly as individual
outer documents; each reusable UserControl owns its nested docking workspace.
Working directory and Output are global tools. View restores global and active
editor panes through the existing editor APIs. Layouts persist per editor type
and entry application. No GameHost is needed for editing.
EngineState project composition remains out of scope. The deprecated Avalonia
Studio prototype and its duplicate editor stack have been retired.

## Shared tooling infrastructure

- [`Shared`](./Shared/)  
  Source-linked tooling infrastructure shared by standalone editors. The current
  WinForms helper provides the editor-local DockPanelSuite workspace used for
  nested, non-persistent docking.

## Repository scripts

- [`scripts`](./scripts/)  
  PowerShell helpers for setup, changelog generation, publishing, deployment,
  release workflows, and other repository maintenance.  
  See: [`scripts/README.md`](./scripts/README.md)

## Runtime boundary

These projects are development tooling. Gondwana games do not require the
authoring applications or Studio at runtime. File formats such as GAF, GTS, GANI,
GSND, and GSCN are defined by the engine/runtime projects; the tools edit those
formats rather than introducing editor-only equivalents.

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
no source-document paths or unsaved content are stored, and documents are never reopened
on startup. Missing or corrupt preferences fall back to defaults without a dialog.
Available plugin tools in Studio use stable plugin identities; missing plugins are ignored.
Changes are saved after a short settling interval and flushed on disposal.
