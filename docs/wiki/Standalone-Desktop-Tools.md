# Standalone Desktop Tools

Gondwana provides focused Windows applications for each native authoring format, plus a separate runtime Scene Viewer.

The standalone editors are not reduced copies of Gondwana Studio. Their reusable editor controls are the authoritative authoring surfaces that Studio hosts directly.

## Native content editors

| Format | Project | Primary job |
| --- | --- | --- |
| GAF / ZIP | `Gondwana.Tooling.Assets.WinForms` | Create and manage typed asset containers. |
| GTS | `Gondwana.Tooling.Tilesheets.WinForms` | Author tilesheets, regions, frames, collision metadata, and image sources. |
| GANI | `Gondwana.Tooling.Animations.WinForms` | Author animation definitions and GTS-backed frame sequences. |
| GSND | `Gondwana.Tooling.Audio.WinForms` | Author portable sound definitions and source metadata. |
| GSCN | `Gondwana.Tooling.Scenes.WinForms` | Author scenes, SceneLayers, sparse tile placements, GTS/GANI sources, and scene preview. |
| GSPR | `Gondwana.Tooling.Sprites.WinForms` | Author collections of sprite definitions with scene/layer and frame references. |

Run a tool from the repository root with the normal `dotnet run --project ...` command. For example:

```console
dotnet run --project Tooling/Gondwana.Tooling.Tilesheets.WinForms -c Release
```

Replace the project name with the editor you want to run.

## Shared architecture

The common pattern is:

```text
standalone MainForm
    |
    +-- working-directory / source browser
    |
    +-- one or more outer editor documents
            |
            +-- reusable editor UserControl
                    |
                    +-- editor-local DockPanelSuite panes
```

The same reusable `UserControl` is what [[Gondwana Studio]] embeds.

This means fixes to an editor's authoring behavior should normally be made in the editor itself, not separately in Studio.

## Format documentation remains authoritative

The standalone applications edit native Gondwana definitions; they do not define separate tooling-only formats.

Use the corresponding format pages for serialization and runtime semantics:

- [[Assets Files]]
- [[.gts Files|GTS-Files]]
- [[.gani Files|GANI-Files]]
- [[.gsnd Files|GSND-Files]]
- [[.gscn Files|GSCN-Files]]
- [[.gspr Files|GSPR-Files]]

The editor applications add authoring workflows such as source browsing, previews, property editing, validation, docking, and Save As.

## Working directories and documents

The standalone editors use a working-directory or source-browser model appropriate to their content type. Multiple authoring documents can remain open at once where the editor supports it.

Editor-specific behavior remains with the editor. Examples include:

- GAF entry import/export/filtering and password-protected containers;
- GTS image and packed-image browsing;
- GANI GTS source browsing and animation preview;
- GSND portable audio-source editing without requiring a backend just to edit;
- GSCN scene/layer editing, GTS/GANI source browsing, sparse tile editing, and definition-driven preview;
- GSPR sprite collection editing, GSCN layer assignment, GTS frame assignment, and sprite preview.

## Docking and layout preferences

The newer standalone editors use an outer application workspace plus an inner editor-owned docking workspace.

Layouts are stored under:

```text
%LOCALAPPDATA%/Hidden Worlds Games/Gondwana/Tooling/<entry-application>/Docking/
```

Editor layout preferences are scoped by entry application, so a standalone editor and Studio can arrange the same editor type differently.

These preferences affect only UI layout. They do not alter native definitions or EngineState.

## Scene Viewer

`Gondwana.Tooling.SceneViewer.WinForms` is a separate utility rather than another definition editor.

Its purpose is:

> Show what a saved authored scene looks like when the Gondwana runtime actually loads and renders it.

Launch it directly with:

```powershell
Gondwana.Tooling.SceneViewer.WinForms.exe --scene "Content\Scenes\level1.gscn"
```

or use **View Scene** from the GSCN editor. The command is available in both the standalone Scene editor and Studio because it belongs to the shared `SceneEditorControl`.

The viewer:

- validates and materializes the saved GSCN;
- resolves authored GTS/GANI dependencies;
- discovers applicable adjacent GSPR definitions according to the viewer's documented resolution rules;
- uses the WinForms GPU host;
- runs real animation playback and Gondwana rendering;
- supports camera movement and zoom;
- can show the runtime diagnostics overlay.

Common controls are:

| Input | Action |
| --- | --- |
| W/A/S/D or arrows | Move camera |
| Shift | Faster movement |
| Mouse wheel | Zoom |
| Home | Reset camera and zoom |
| F3 | Toggle runtime diagnostics |
| F4 | Pause/resume active tile animations |
| Esc | Close |

The viewer is read-only. There is no save-back, gameplay scripting, hot reload, or editor transport.

See the Runtime Scene Viewer section of [[.gscn Files|GSCN-Files]] for dependency resolution and runtime behavior.

## Studio or standalone?

Use [[Gondwana Studio]] when you want several content types open under one shell and want shared global tools or plugins.

Use a standalone editor when the task is focused on one format, when you are testing that editor independently, or when you prefer a smaller dedicated application.

The content model is the same either way.

## Where to read next

- [[Studio & Desktop Tools]]
- [[Gondwana Studio]]
- [[External Asset Importing]]
- [[.gscn Files|GSCN-Files]]
