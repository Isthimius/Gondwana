# Gondwana Tooling

Gondwana provides authoring applications, command-line utilities, reusable asset
libraries, and development scripts. **The engine is code-first:** games are normal
.NET projects and do not require Gondwana Studio or the standalone editors.

## Choose a workflow

| Goal | Start here |
| --- | --- |
| Author GAF, GTS, GANI, GSND, GSCN, and GSPR content in one workspace | [Gondwana Studio and desktop editors](./Studio/README.md) |
| Edit a single content format | [Standalone editors](./Studio/README.md#standalone-editors) |
| Render a saved scene in the engine | [Scene Viewer](./Studio/Gondwana.Tooling.Studio.SceneViewer.WinForms/README.md) |
| Create, inspect, run, publish, or deploy from a terminal | [Gondwana CLI](./Gondwana.Cli/README.md) |
| Start a WinForms, Avalonia, or Blazor game | [Project templates](./Gondwana.Templates/README.md) |
| Work with Gondwana from a compatible AI client | [Gondwana MCP](./Gondwana.Mcp/README.md) |

The [Studio overview](./Studio/README.md) is the home for the Windows release
bundle layout, individual editors, plugins, shared WinForms infrastructure, and
workspace preferences. Individual project READMEs provide usage and development
details.

## Reusable asset libraries

These projects provide non-UI workflows. They are separate from the Studio shell
and can be used by other tooling.

| Project | Responsibility |
| --- | --- |
| [Gondwana.Tooling.Assets](./Gondwana.Tooling.Assets/README.md) | Semantic GAF packaging, unpacking, imports, reference discovery, relocation, and deletion |
| [Gondwana.Tooling.Importers](./Gondwana.Tooling.Importers/) | Headless conversion of supported Tiled, Godot, and Aseprite assets into native Gondwana definitions |

Studio's [External Asset Importer](./Studio/Plugin/Gondwana.Tooling.Studio.Plugin.ExternalImport/README.md)
uses the Importers library; it is not a separate conversion implementation.

For the native asset and reference contracts, see [Assets Files](../docs/wiki/Assets-Files.md),
[Reference Relocation](../docs/wiki/Reference-Relocation.md), and
[External Asset Importing](../docs/wiki/External-Asset-Importing.md).

## Development and repository maintenance

| Project or directory | Purpose |
| --- | --- |
| [Gondwana.Cli](./Gondwana.Cli/README.md) | `gondwana` commands, environment checks, and deployment |
| [Gondwana.Templates](./Gondwana.Templates/README.md) | `dotnet new` templates and generated project setup |
| [Gondwana.Mcp](./Gondwana.Mcp/README.md) | Read-only access to the official repository and wiki for supported AI clients |
| [scripts](./scripts/README.md) | PowerShell setup, changelog, publishing, release, and maintenance helpers |

Windows authoring releases and their packaging scripts are described in the
[Studio overview](./Studio/README.md#windows-release-bundles).
