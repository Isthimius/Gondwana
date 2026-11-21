# Gondwana Studio and desktop editors

This directory contains Gondwana's Windows content-authoring applications,
reusable WinForms editors, Studio plugin infrastructure, and shared UI support.
All tools work with the **engine's native definition formats**; they do not
introduce a separate Studio project format or require a running game or `GameHost`
for editing.

For the CLI, templates, headless asset libraries, MCP, and repository scripts,
see the [Tooling overview](../README.md).

## Applications

[**Gondwana Studio**](./Gondwana.Tooling.Studio.WinForms/README.md) combines six
hostable editors in a dockable, multi-document workspace, with working-directory
navigation, save commands, dependency inspection, and optional plugins.
The standalone applications use those **same editor controls**; there is no
second set of format-specific editors inside Studio.

### Standalone editors

| Format | Project | Purpose |
| --- | --- | --- |
| GAF / ZIP | [Assets](./Gondwana.Tooling.Studio.Assets.WinForms/README.md) | Package contents, import/export, relocation, references, and encryption |
| GTS | [Tilesheets](./Gondwana.Tooling.Studio.Tilesheets.WinForms/README.md) | Tilesheets, image regions, frames, and collision metadata |
| GANI | [Animations](./Gondwana.Tooling.Studio.Animations.WinForms/README.md) | Animation cycles, frames, and previews |
| GSND | [Audio](./Gondwana.Tooling.Studio.Audio.WinForms/README.md) | Sound definitions and source references |
| GSCN | [Scenes](./Gondwana.Tooling.Studio.Scenes.WinForms/README.md) | Scenes, layers, placements, and authoring preview |
| GSPR | [Sprites](./Gondwana.Tooling.Studio.Sprites.WinForms/README.md) | Sprite definitions, scene/layer and frame references |

GTS, GANI, GSND, GSCN, and GSPR support multiple logical definitions per file.
For exact editing behavior and limitations, consult each editor's README.

### Runtime Scene Viewer

[**Scene Viewer**](./Gondwana.Tooling.Studio.SceneViewer.WinForms/README.md)
loads a saved GSCN into a real GPU-backed Gondwana runtime. It differs from
the GSCN editor's lightweight authoring preview. Studio and the standalone
Scenes editor can launch it using **View Scene**; the viewer provides camera
movement, zoom, animation pause, and runtime diagnostics.

## Running from source

From the repository root on Windows, using the appropriate .NET SDK and
Windows desktop build tools:

```powershell
dotnet run --project Tooling/Studio/Gondwana.Tooling.Studio.WinForms -c Release
dotnet run --project Tooling/Studio/Gondwana.Tooling.Studio.Tilesheets.WinForms -c Release
```

Replace the second project with another standalone editor or the Scene Viewer
as needed. See each project README for application-specific commands and options.

## Windows release bundles

Tagged GitHub Releases provide `Gondwana-<version>-binaries.zip`. Extract the
**entire** archive. The eight published Windows applications appear as sibling
directories beneath `Tooling/` in the release bundle:

```text
Tooling/
  Gondwana.Tooling.Studio.WinForms/
    Gondwana.Tooling.Studio.WinForms.exe
    plugins/
      ProjectDiagnostics/
      ExternalImport/
  Gondwana.Tooling.Studio.Assets.WinForms/
  Gondwana.Tooling.Studio.Animations.WinForms/
  Gondwana.Tooling.Studio.Audio.WinForms/
  Gondwana.Tooling.Studio.Scenes.WinForms/
  Gondwana.Tooling.Studio.SceneViewer.WinForms/
  Gondwana.Tooling.Studio.Sprites.WinForms/
  Gondwana.Tooling.Studio.Tilesheets.WinForms/
```

Each application directory contains its own executable and runtime dependencies.
The Windows x64 builds are **self-contained** and portable: a separate .NET
Desktop Runtime is not required, and these are folder distributions rather than
single-file executables or installers. Keep each directory intact. In particular,
the Scene Viewer must remain a sibling of Studio and the Scenes editor for
**View Scene** to locate it.

The authoritative publishing and bundle-validation scripts are
[Publish-WindowsTooling.ps1](../scripts/Publish-WindowsTooling.ps1) and
[Test-WindowsToolingBundle.ps1](../scripts/Test-WindowsToolingBundle.ps1).

## Project structure and reuse

| Component | Responsibility |
| --- | --- |
| [Studio WinForms shell](./Gondwana.Tooling.Studio.WinForms/README.md) | Outer workspace, file navigation, documents, menus, and plugin integration |
| [Studio Core](./Gondwana.Tooling.Studio.Core/) | Framework-neutral Studio contracts and shell state |
| [Shared WinForms](./Shared/WinForms/README.md) | Editor docking, workspace sessions, navigation preferences, and shared controls |
| [Shared theming](./Shared/WinForms/Theming/README.md) | Appearance and theme preferences |
| [Plugin projects](./Plugin/) | Optional Studio extensions built and deployed separately |

Native file loading and serialization remain in Gondwana. The UI-independent
[Gondwana.Tooling.Assets](../Gondwana.Tooling.Assets/README.md) library supplies
authoring operations also used by the CLI; the
[Gondwana.Tooling.Importers](../Gondwana.Tooling.Importers/) library provides
conversion services for supported external formats.

## Studio plugins

Studio's six editors are built in and work without plugins. The distribution
includes two optional extensions:

| Plugin | Purpose |
| --- | --- |
| [Project Diagnostics](./Plugin/Gondwana.Tooling.Studio.Plugin.ProjectDiagnostics/README.md) | Read-only inspection of authored files, explicit references, and validation issues |
| [External Asset Importer](./Plugin/Gondwana.Tooling.Studio.Plugin.ExternalImport/README.md) | Analyze and convert supported Tiled, Godot, and Aseprite content using the shared Importers library |

Plugins are discovered at Studio startup from its `plugins/` directory.
**Plugins > Manage Plugins…** displays discovery diagnostics and supports
user-reviewed third-party package operations; activation and package changes
take effect after restarting Studio. Bundled plugins may be disabled but are
maintained by the Studio distribution. For package conventions and deployment,
see [Studio Plugin Packages](../../docs/wiki/Studio-Plugin-Packages.md).

## Workspace preferences

Studio and the standalone editors retain independent per-user dock layouts.
The Studio shell and each editor format have separate layout profiles under
`%LOCALAPPDATA%/Hidden Worlds Games/Gondwana/Tooling/<entry-application>/Docking/`.
Use **View > Reset application layout** or **Reset active editor layout**
to restore default panes.

Studio can also restore its working directory and previously saved documents
at startup. Unsaved edits and untitled documents are **not** recovered.
These preferences do not modify Gondwana asset files. See the
[Studio README](./Gondwana.Tooling.Studio.WinForms/README.md#workspace-session-restoration)
and [shared docking documentation](./Shared/WinForms/README.md) for details.

## Further documentation

The wiki covers [Studio](../../docs/wiki/Gondwana-Studio.md),
[Studio and desktop tools](../../docs/wiki/Studio-and-Desktop-Tools.md),
[standalone desktop tools](../../docs/wiki/Standalone-Desktop-Tools.md),
and [native asset files](../../docs/wiki/Assets-Files.md).
