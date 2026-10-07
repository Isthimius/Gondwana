# Studio & Desktop Tools

Gondwana is code-first and does not require an editor, but it includes a Windows authoring layer for the native content formats used by the engine.

The central design rule is simple:

> The standalone content editors are the authoritative authoring surfaces. Gondwana Studio hosts those same reusable editor controls in one shell rather than maintaining a second editor implementation.

That keeps Studio optional without making it a parallel content system.

## Choose an entry point

| Tool | Use it when |
| --- | --- |
| [[Gondwana Studio]] | You want one integrated Windows workspace for several Gondwana content formats, working-directory browsing, shared shell tools, and optional plugins. |
| [[Standalone Desktop Tools]] | You want a focused application for one content type, or you want to use/test an editor independently of Studio. |
| Scene Viewer | You want to see a saved GSCN materialized and rendered through the real Gondwana runtime rather than the lightweight authoring preview. |
| [[External Asset Importing]] | You want to convert supported Godot, Tiled, or Aseprite content into native Gondwana authoring files. |

All of these are development tools. A game does not need Studio or the standalone applications at runtime.

## Authoring formats

Studio and the standalone editors operate on the same native formats consumed by Gondwana:

| Format | Purpose | Standalone editor | Format documentation |
| --- | --- | --- | --- |
| GAF / ZIP | Typed asset containers | `Gondwana.Tooling.Assets.WinForms` | [[Assets Files]] |
| GTS | Tilesheet definitions | `Gondwana.Tooling.Tilesheets.WinForms` | [[.gts Files|GTS-Files]] |
| GANI | Animation definitions | `Gondwana.Tooling.Animations.WinForms` | [[.gani Files|GANI-Files]] |
| GSND | Audio definitions | `Gondwana.Tooling.Audio.WinForms` | [[.gsnd Files|GSND-Files]] |
| GSCN | Scene definitions | `Gondwana.Tooling.Scenes.WinForms` | [[.gscn Files|GSCN-Files]] |
| GSPR | Sprite collections | `Gondwana.Tooling.Sprites.WinForms` | [[.gspr Files|GSPR-Files]] |

The format pages remain authoritative for serialization, runtime behavior, provenance, dependency rules, and validation semantics. Tooling documentation explains how those formats are edited.

## Shared editor architecture

Each standalone authoring application exposes its main editor as a reusable WinForms `UserControl`. Studio embeds those same controls as documents:

```text
native format
    |
    v
definition/document model
    |
    v
reusable editor control
    |
    +--> standalone WinForms shell
    |
    +--> Gondwana Studio
```

Studio therefore does not translate files into Studio-specific models and does not own alternate serializers.

Each editor owns its own inner DockPanelSuite workspace and panes. The standalone application or Studio owns the outer application/document workspace.

## Gondwana Studio

[[Gondwana Studio]] combines all six native content editors in one Windows shell.

Studio provides:

- a working-directory browser;
- multi-document editing;
- File/Open/New/Save/Save As/Close routing for the active document;
- global Output and plugin panes;
- per-editor pane restoration through the View menu;
- per-user dock-layout persistence;
- optional plugin loading from the Studio `plugins/` directory.

Studio does not require a `GameHost` or a running game loop for normal authoring.

## Standalone editors

[[Standalone Desktop Tools]] remain first-class applications rather than compatibility wrappers. They are useful when:

- editing one content type without launching the whole Studio shell;
- testing an editor in isolation;
- embedding or maintaining one editor surface;
- keeping separate layout preferences for a focused workflow.

Studio and the standalone applications store independent UI-layout profiles even though they reuse the same editor controls.

## Scene Viewer

The Scene Viewer is different from the definition editors.

`Gondwana.Tooling.SceneViewer.WinForms` opens a saved GSCN, resolves its authored dependencies, materializes an actual runtime `Scene`, and renders it through the current WinForms GPU host.

The GSCN editor's **View Scene** command launches the viewer in a separate process from either Studio or the standalone Scene editor. The lightweight Scene Preview remains definition-driven and editable; the Scene Viewer is the runtime check.

See [[.gscn Files|GSCN-Files]] for the GSCN model and the Runtime Scene Viewer section.

## Plugins and importing

Studio supports optional plugins without moving core editing functionality out of the built-in editors.

Current repository plugins include:

- **Project Diagnostics** — read-only inspection of native authoring files and their explicit references in the active working directory.
- **External Asset Importer** — converts supported Godot 3.x/4.x TileSets, Tiled TSX/TMX content, and Aseprite sprites into native Gondwana definitions and generated PNG assets.

The import pipeline is headless beneath the Studio plugin, so foreign-format knowledge stays outside the native editors and runtime serializers.

See [[External Asset Importing]] for the supported conversion matrix and format-specific limits.

## Runtime boundary

Studio and the desktop tools edit the same files that the engine can load without those applications being present:

```text
Studio / standalone editor
        |
        v
native Gondwana definition
        |
        v
runtime serializer / loader
        |
        v
game
```

There is no Studio project format required by the engine, and EngineState project composition is separate from the current Studio shell.

## Where to read next

- [[Gondwana Studio]]
- [[Standalone Desktop Tools]]
- [[External Asset Importing]]
- [[Assets Files]]
- [[.gts Files|GTS-Files]]
- [[.gani Files|GANI-Files]]
- [[.gsnd Files|GSND-Files]]
- [[.gscn Files|GSCN-Files]]
- [[.gspr Files|GSPR-Files]]
