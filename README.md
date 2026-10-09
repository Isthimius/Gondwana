# Gondwana Game Engine

[![NuGet](https://img.shields.io/nuget/v/Gondwana)](https://www.nuget.org/packages/Gondwana)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Gondwana)](https://www.nuget.org/packages/Gondwana)
[![License](https://img.shields.io/github/license/Isthimius/Gondwana)](https://github.com/Isthimius/Gondwana/blob/master/LICENSE)
[![Docs](https://img.shields.io/badge/docs-wiki-blue)](https://github.com/Isthimius/Gondwana/wiki)
[![API](https://img.shields.io/badge/api-reference-blue)](https://isthimius.github.io/Gondwana/api/)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Platforms](https://img.shields.io/badge/platforms-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20WebAssembly-blue)

<img alt="Gondwana logo" src="https://github.com/user-attachments/assets/cefd03d0-de2b-474e-8f72-e4ab672cede3" align="left" width="40%" />

**Gondwana** is a code-first, cross-platform 2D and 2.5D game and rendering engine for C# and .NET 8. It gives developers fine-grained control over rendering, timing, movement, input, collision detection, scene composition, and game architecture without requiring an editor to own the project.

Gondwana targets Windows, Linux, macOS, and WebAssembly through SkiaSharp-based rendering, with integrations for WinForms, Avalonia, and Blazor. It supports CPU bitmap and GPU-backed rendering, including OpenGL on desktop and a dedicated WebGL path for Blazor/WebAssembly. Layered worlds support multiple views, parallax, stable z-ordering, particles, multiple grid projections, game UI widgets, audio, video, gamepad input, and reusable content definitions.

Game behavior remains ordinary C#. **Gondwana Studio** and the standalone authoring tools provide optional visual workflows for assets, tilesheets, animations, sounds, scenes, and sprites without requiring those tools to own the application or runtime architecture.

The engine carries forward the predictability of classic rendering systems—explicit composition, stable ordering, understandable timing, and direct access to the pipeline—inside a modern, modular .NET architecture.

<br clear="left" />

## 🎮 Gondwana in Action

### [Spot!](Demos/Spot.WinForms)

[▶ Play Spot! in your browser](https://isthimius.itch.io/spot) · [View source](Demos/Spot.Shared)

<p>
  <img width="49%" alt="Spot gameplay showing the game board and HUD" src="https://github.com/user-attachments/assets/c29ddd87-fb82-46dc-ad5e-6388c11ba50d" />
  <img width="49%" alt="Spot gameplay showing animated scene rendering" src="https://github.com/user-attachments/assets/0aef0b63-1c16-44be-b6a6-d456f4799ce8" />
</p>

## Get Started

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```console
dotnet new install Gondwana.Templates
dotnet new gondwana-winforms -n MyFirstGame
cd MyFirstGame
dotnet run
```

For a guided introduction, see **[Make Your First Game in 30 Minutes with Gondwana](https://github.com/Isthimius/Gondwana/wiki/Make-Your-First-Game-in-30-Minutes)**.

> [!NOTE]
> Gondwana is actively developed. Its public API is usable today, but breaking changes may occur between versions as the engine and tooling mature.

## Choose Your Workflow

Gondwana supports code-first development and optional visual authoring. These workflows can be used independently or together.

### 💻 Code First

Build directly in C# using Gondwana's NuGet packages, project templates, and `gondwana` CLI.

Game code has direct access to Gondwana's rendering, scene, input, movement, collision, audio, widgets, video, and hosting APIs.

### 🛠️ Visual Authoring with Gondwana Studio

**Gondwana Studio** is the Windows authoring environment for Gondwana's persistent content-definition formats. It provides multi-document editing, visual previews, validation, dependency-aware authoring, docking, and persisted workspace layouts.

<img width="100%" alt="image" src="https://github.com/user-attachments/assets/15dbcd65-2c6a-44e8-9038-b7db816dd736" />

Studio currently authors:

| Format   | Content                                             |
| -------- | --------------------------------------------------- |
| **GAF**  | Asset packages                                      |
| **GTS**  | Tilesheets, regions, frames, and collision metadata |
| **GANI** | Animation definitions                               |
| **GSND** | Sound definitions                                   |
| **GSCN** | Scenes and SceneLayers                              |
| **GSPR** | Sprite definitions                                  |

These are Gondwana content formats rather than Studio-specific project files. Applications can load them at runtime or construct the equivalent engine objects directly in C#.

Studio and the standalone editors share the same underlying authoring controls and definition models.

See **[Gondwana Studio](Tooling/Gondwana.Tooling.Studio.WinForms)** for details.

---

## Documentation & Resources

- 📘 **[Engine Wiki](https://github.com/Isthimius/Gondwana/wiki)**
- 📚 **[API Reference — stable](https://isthimius.github.io/Gondwana/api/)** · [Development (`master`)](https://isthimius.github.io/Gondwana/api/latest/) · Historical releases are available in the API version selector.
- 🤖 **[Using Gondwana with ChatGPT and Codex](https://github.com/Isthimius/Gondwana/wiki/Using-Gondwana-with-ChatGPT-and-Codex)**
- 🛠️ **[Gondwana Studio](Tooling/Gondwana.Tooling.Studio.WinForms)**
- 📦 **[NuGet Package](https://www.nuget.org/packages/Gondwana)**
- 🏷️ **[GitHub Releases](https://github.com/Isthimius/Gondwana/releases)**
- 📜 **[Release History](https://github.com/Isthimius/Gondwana/blob/master/CHANGELOG.md)**
- ✅ **[Latest CI Run](https://github.com/Isthimius/Gondwana/actions/workflows/ci-master.yml)**
- 💬 **[Discussions](https://github.com/Isthimius/Gondwana/discussions)**
- 📣 **[What’s New in Gondwana 2.6](https://github.com/Isthimius/Gondwana/discussions/366)**

---

## 🎯 Who Gondwana Is For

Gondwana is designed for .NET developers who want the convenience of an engine without giving up control of their application's code and architecture.

It is a good fit when you value:

- Fine-grained control over rendering, timing, input, and movement
- Predictable and debuggable draw and update pipelines
- A code-first C# workflow
- Optional visual content authoring
- Cross-platform desktop and WebAssembly targets
- A reusable foundation for custom 2D and 2.5D games
- Modular packages that can be adopted independently

Gondwana is an engine and framework rather than an all-encompassing visual game-making suite. Runtime behavior and application structure remain under the developer's control.

---

## ✨ Features

- **Cross-platform SkiaSharp rendering** through CPU bitmap and GPU-backed surfaces
- **Project and deployment CLI** for scaffolding, environment checks, package upgrades, asset workflows, desktop/WebAssembly publishing, and deployment to static web hosts and itch.io
- **Backbuffer abstraction** through `BitmapBackbuffer` and `GpuBackbuffer`
- **WinForms, Avalonia, and Blazor adapters**, with ready-to-use hosts for Windows, Linux, macOS, and WebAssembly
- **View-centric layered scenes** with multiple cameras, viewports, parallax, stable z-ordering, and world-space dirty-region tracking
- **Host-owned display effects** for view- and layer-level effects, including fades, slides, directional fills and erases, view shake, and zoom
- **Modular lighting and fog-of-war primitives**, including radial lights, flicker, darkness overlays, and tracked world-space reveal areas
- **Multiple coordinate systems**: orthogonal, rhombic isometric, axial isometric, flat-top hex, pointy-top hex, left oblique, and right oblique
- **Sprites and DirectDrawing** for reusable images, shapes, text, particles, overlays, effects, composites, and high-volume bitmap instances
- **Reusable game UI widgets** with lifecycle events, automatic input registration, focus, keyboard and pointer routing, dragging, hit testing, and components such as `SplashScreen`
- **Sprite and camera movement** with easing, target following, interpolation, scripted paths, and reusable visual effects
- **Collision detection and kinematic resolution**, including per-frame and per-tile collision adjustments for more precise collision geometry
- **High-resolution timing** and thread-safe drawable management with stable z-order sorting
- **Asset support** for tilesheets, sprites, fonts, audio, and packaged Gondwana asset files
- **Unified keyboard, mouse, touch, and gamepad input**, with SDL2 gamepad support available as a dedicated package
- **Audio and MIDI support** through optional desktop and browser backends
- **Native desktop video playback** through LibVLCSharp, including `DirectVideo` rendering and optional interactive `VideoWidget` integration
- **First-class authoring formats** for packaged assets (GAF), tilesheets (GTS), animations (GANI), sounds (GSND), scenes (GSCN), and sprites (GSPR)
- **Gondwana Studio** for integrated multi-document authoring, visual previews, validation, dependency-aware workflows, reusable docked editors, and persistent layouts
- **AI-assisted development** through the official Gondwana Game Engine plugin for ChatGPT and Codex, grounded in the current source, tests, and documentation

---

## 🎬 More Demos

### [Platformer Demo](Demos/Gondwana.Platformer)

<img width="800" alt="Gondwana platformer gameplay demonstration" src="https://github.com/user-attachments/assets/fc7e5a89-dcd1-4d85-ab6c-071190336a0f" />

### [Space Shooter Demo](Demos/Gondwana.SpaceDuel)

<img width="800" alt="Gondwana space-dueling gameplay demonstration" src="https://github.com/user-attachments/assets/58eadce8-d5f6-4e2d-a97a-fdeee142eab0" />

### [Particle Test](Demos/Gondwana.ParticleTest)

<img width="800" alt="Gondwana particle-system demonstration" src="https://github.com/user-attachments/assets/105740af-e8e5-4f92-92e2-7986612008a1" />

### [Coordinate-System Test](Demos/Gondwana.CoordinateTest)

<img width="800" alt="Gondwana coordinate-system demonstration" src="https://github.com/user-attachments/assets/6ae8183e-b4e6-4740-9a01-9679ed66cd40" />

---

## 📂 Architecture

### Runtime architecture

Gondwana uses a central `Engine` cycle to advance timing, input, movement, animation, and game state. Active `View` instances project and composite `SceneLayer` contents through cameras and viewports into a platform backbuffer.

Platform adapters for WinForms, Avalonia, and Blazor handle presentation and native input at the edges, leaving the core engine platform-agnostic.

```text
Engine
  ↓
Views and Cameras
  ↓
SceneLayers and Drawables
  ↓
Backbuffer
  ↓
Platform Adapter
```

See the **[Engine Wiki](https://github.com/Isthimius/Gondwana/wiki)** for detailed rendering pipelines and subsystem documentation.

### Authoring architecture

Applications can configure Gondwana directly in C# or load persistent content definitions created by Studio and the standalone authoring tools.

```text
                    Authoring / Development
                              │
                              ▼
              ┌───────────────────────────────┐
              │                               │
          C# directly                 Visual authoring
              │                               │
              │                         Gondwana Studio
              │                               │
              │                       Definition formats
              │                   GAF / GTS / GANI / GSND /
              │                          GSCN / GSPR
              │                               │
              └───────────────┬───────────────┘
                              │
                              ▼
                         Gondwana APIs
                              │
                              ▼
                         Engine runtime

               ChatGPT / Codex can assist either workflow
```

Studio, standalone tools, and applications use the same public definition models and serializers. The ChatGPT/Codex integration provides development-time assistance and is not part of the game runtime.

## 📦 Packages

Runtime packages are available on NuGet. Install only the pieces your project needs.

| Package | Description |
| --- | --- |
| [`Gondwana`](https://www.nuget.org/packages/Gondwana) | Core engine; required by Gondwana projects |
| [`Gondwana.Hosting`](https://www.nuget.org/packages/Gondwana.Hosting) | Cross-platform `GameHostBase` for structured startup, shutdown, and lifecycle management |
| [`Gondwana.Widgets`](https://www.nuget.org/packages/Gondwana.Widgets) | Reusable game UI widgets, overlays, HUD elements, and unified widget input routing |
| [`Gondwana.WinForms`](https://www.nuget.org/packages/Gondwana.WinForms) | WinForms rendering and input adapters for Windows |
| [`Gondwana.WinForms.Hosting`](https://www.nuget.org/packages/Gondwana.WinForms.Hosting) | Ready-to-use `WinFormsGameHost` |
| [`Gondwana.Avalonia`](https://www.nuget.org/packages/Gondwana.Avalonia) | Avalonia rendering and input adapters for Windows, macOS, and Linux |
| [`Gondwana.Avalonia.Hosting`](https://www.nuget.org/packages/Gondwana.Avalonia.Hosting) | Ready-to-use `AvaloniaGameHost` |
| [`Gondwana.Blazor`](https://www.nuget.org/packages/Gondwana.Blazor) | Blazor WebAssembly rendering, input, and browser components |
| [`Gondwana.Blazor.Hosting`](https://www.nuget.org/packages/Gondwana.Blazor.Hosting) | Ready-to-use Blazor host lifecycle integration |
| [`Gondwana.Input.SDL2`](https://www.nuget.org/packages/Gondwana.Input.SDL2) | Cross-platform SDL2 gamepad input; requires native SDL2 |
| [`Gondwana.Audio.NAudio`](https://www.nuget.org/packages/Gondwana.Audio.NAudio) | Windows audio backend for the common core audio API, including Vorbis and variable playback speed |
| [`Gondwana.Audio.Midi`](https://www.nuget.org/packages/Gondwana.Audio.Midi) | Windows MIDI playback and SoundFont support layered on the NAudio backend |
| [`Gondwana.Audio.Browser`](https://www.nuget.org/packages/Gondwana.Audio.Browser) | Browser and WebAssembly audio through the HTML5 Audio API and JavaScript interop |
| [`Gondwana.Video`](https://www.nuget.org/packages/Gondwana.Video) | Native desktop video playback through LibVLCSharp |
| [`Gondwana.Video.Widgets`](https://www.nuget.org/packages/Gondwana.Video.Widgets) | Optional interactive and draggable `VideoWidget` bridge between Gondwana.Video and Gondwana.Widgets |

---

## 🧰 Tooling

Gondwana's development tooling is optional and is not required by the engine at runtime.

| Tool | Install / location | Description |
| --- | --- | --- |
| **Gondwana Studio** | [`Tooling/Gondwana.Tooling.Studio.WinForms`](Tooling/Gondwana.Tooling.Studio.WinForms) | Integrated Windows authoring environment for GAF, GTS, GANI, GSND, GSCN, and GSPR content |
| **Standalone editors** | `Tooling/Gondwana.Tooling.*.WinForms` | Individual authoring utilities for assets, tilesheets, animations, sounds, scenes, and sprites, using the same reusable controls hosted by Studio |
| **Gondwana.Templates** | `dotnet new install Gondwana.Templates` | Project templates for `gondwana-winforms`, `gondwana-avalonia`, and `gondwana-blazor` |
| **Gondwana.Cli** | `dotnet tool install --global Gondwana.Cli` | The `gondwana` CLI for creating, checking, upgrading, running, publishing, and deploying Gondwana projects; managing and validating assets; and one-command Blazor/WebAssembly deployment to static web hosts or itch.io |
| **Gondwana Game Engine plugin** | ChatGPT / Codex Plugin Directory | Engine-aware AI assistance using the current public Gondwana source, tests, and wiki |
| **Gondwana.Mcp** | [`Tooling/Gondwana.Mcp`](Tooling/Gondwana.Mcp) | Read-only MCP service that powers the official Gondwana AI integration |

> **Platform note:** Gondwana Studio and the standalone authoring editors currently use WinForms and therefore run on Windows. Gondwana games themselves can target Windows, Linux, macOS, and WebAssembly through the appropriate platform adapters.

### Publish and deploy from the CLI

Blazor/WebAssembly games can be published and deployed directly from the command line:

```console
gondwana publish blazor
gondwana deploy itch
```

Static web-host deployment is also supported.

### ChatGPT and Codex

The official **Gondwana Game Engine** plugin gives ChatGPT and Codex access to current public Gondwana source, tests, and documentation.

You can use it to ask engine-specific questions or give Codex implementation tasks grounded in the current engine rather than relying solely on model training data or examples from unrelated game engines.

See **[Using Gondwana with ChatGPT and Codex](https://github.com/Isthimius/Gondwana/wiki/Using-Gondwana-with-ChatGPT-and-Codex)**.

---

## 🧭 Key Design Principles

- **Code first**: Game behavior and application architecture remain ordinary C#.
- **Optional tooling**: Visual editors author reusable content without owning the application.
- **World space first**: Gameplay and scene logic operate in world coordinates; views and cameras handle projection.
- **Dirty-region rendering where it pays**: CPU bitmap backbuffers can redraw changed world-space regions rather than repainting the entire frame.
- **Layered, view-centric scenes**: Scenes contain independently rendered `SceneLayer` instances, while views support multiple cameras and viewports.
- **Adapters at the edges**: Platform projects host rendering surfaces and native input while the core remains platform-agnostic.
- **Explicit composition**: Sprites, direct drawings, composites, widgets, and scene layers have clear ownership and ordering rules.
- **Predictable behavior**: Stable ordering and explicit timing keep rendering and movement understandable and debuggable.
- **Reusable content definitions**: GAF, GTS, GANI, GSND, GSCN, and GSPR provide persistent engine content where useful.
- **Modular architecture**: Hosting, widgets, platform adapters, audio, input, video, tooling, and AI integration remain separate packages and systems.

---

## 🛠 Roadmap

_Gondwana is actively evolving, with an emphasis on strengthening the engine, authoring workflow, and runtime systems._

* [x] WebAssembly support through Blazor
* [x] Integrated Gondwana Studio authoring environment
* [x] First-class visual authoring
* [x] Full platformer sample
* [x] WebGL-backed Blazor rendering adapter
* [x] External content import tooling for TMX/TSX tile maps, Godot 3/4 TileSets, and Aseprite assets
* [ ] Expanded 2D physics, including momentum, elasticity, and additional collision shapes
* [ ] Native, first-class pathfinding
* [ ] Initial client/server networking support
* [ ] Android and iOS support via .NET MAUI adapters

---

## 🤝 Contributing

Contributions are welcome.

- Open an issue for bug reports, feature requests, or proposed changes.
- Fork the repository and create a focused branch for your work.
- Keep changes scoped and include or update tests where appropriate.
- Follow the repository's existing formatting and analyzer conventions.
- Submit a pull request describing the problem being addressed and the approach taken.

For larger changes, opening an issue or discussion first is recommended so the intended direction can be agreed upon before significant implementation work begins.

---

## 🌐 Repository Mirrors

GitHub is the canonical repository for [Gondwana](https://github.com/Isthimius/Gondwana). Issues, pull requests, discussions, releases, and development activity should be submitted there.

Read-only mirrors are maintained for availability and discoverability:

- **[Bitbucket](https://bitbucket.org/isthimius/gondwana)** — source mirror
- **[Codeberg](https://codeberg.org/Isthimius/Gondwana)** — source mirror
- **[GitLab](https://gitlab.com/isthimius/gondwana)** — source mirror
- **[SourceForge](https://sourceforge.net/projects/gondwana/)** — source mirror and release downloads

---

## 📜 License

Gondwana is available under the [MIT License](LICENSE).

**Third-party libraries**  
Gondwana uses **[Skia](https://skia.org/)** (© Google), licensed under the [BSD 3-Clause License](https://opensource.org/license/bsd-3-clause), through **[SkiaSharp](https://github.com/mono/SkiaSharp)** (© Microsoft and contributors), licensed under the [MIT License](https://opensource.org/license/mit).

---

## ☕ Support Gondwana

Gondwana is developed and maintained independently. If you find the engine useful, consider [buying me a coffee](https://www.buymeacoffee.com/mikeleeisback) to support its continued development.
