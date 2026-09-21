# Gondwana Game Engine

[![NuGet](https://img.shields.io/nuget/v/Gondwana)](https://www.nuget.org/packages/Gondwana)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Gondwana)](https://www.nuget.org/packages/Gondwana)
[![License](https://img.shields.io/github/license/Isthimius/Gondwana)](https://github.com/Isthimius/Gondwana/blob/master/LICENSE)
[![Docs](https://img.shields.io/badge/docs-wiki-blue)](https://github.com/Isthimius/Gondwana/wiki)
[![API](https://img.shields.io/badge/api-reference-blue)](https://isthimius.github.io/Gondwana/api/)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Platforms](https://img.shields.io/badge/platforms-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20WebAssembly-blue)

<img alt="Gondwana logo" src="https://github.com/user-attachments/assets/cefd03d0-de2b-474e-8f72-e4ab672cede3" align="left" width="40%" />

**Gondwana** is a code-first, cross-platform 2D and 2.5D game and rendering engine for C# and .NET 8. It gives developers fine-grained control over rendering, timing, movement, input, collision detection, scene composition, and game architecture without requiring the editor to own the project.

Gondwana targets Windows, Linux, macOS, and WebAssembly through SkiaSharp-based rendering, with dedicated integrations for WinForms, Avalonia, and Blazor. Its layered worlds support multiple views, parallax, stable z-ordering, particles, pixel overhang, and several grid projections. Optional packages add desktop and browser audio, MIDI playback, SDL2 gamepad input, video playback, hosting, and reusable game UI widgets.

Gondwana remains fundamentally **code-first**: game behavior is ordinary C#, and developers can reach the rendering and update pipeline directly when needed. Visual tooling is additive rather than mandatory. **Gondwana Studio** provides integrated authoring for assets, tilesheets, animations, audio, scenes, and sprites without turning those files into opaque editor-owned project data.

Developers can work directly in code, use Gondwana Studio for content that benefits from visual authoring, or use the official **Gondwana Game Engine** plugin with ChatGPT and Codex for engine-aware assistance grounded in the current public source, tests, and documentation.

The engine carries forward the predictability of classic Win32/GDI-era rendering—explicit draw order, dirty-region updates where appropriate, scene composition, and timing—inside a modern, modular architecture. The result is an engine intended to remain understandable and debuggable without demanding that a project surrender control as it grows.

<br clear="left" />

## 🎮 Gondwana in Action

### [Spot!](Demos/Spot)

Spot! is Gondwana's primary playable showcase.

<p>
  <img width="49%" alt="Spot gameplay showing the game board and HUD" src="https://github.com/user-attachments/assets/c29ddd87-fb82-46dc-ad5e-6388c11ba50d" />
  <img width="49%" alt="Spot gameplay showing animated scene rendering" src="https://github.com/user-attachments/assets/0aef0b63-1c16-44be-b6a6-d456f4799ce8" />
</p>

## 🛠 Gondwana Studio

**Gondwana Studio** is the integrated Windows authoring environment for Gondwana's current persistent content-definition model. It hosts the same reusable editor controls as the standalone utilities, with multi-document editing, visual previews, validation, dependency-aware authoring, nested docking, and persisted workspace layouts.

> **[LARGE SCREENSHOT PLACEHOLDER — Gondwana Studio with several authoring documents open, ideally showing GSCN/GSPR/GTS panes and the dark docked layout.]**

Studio currently authors:

| Format | Content |
| --- | --- |
| **GAF** | Asset packages |
| **GTS** | Tilesheets, regions, frames, and collision metadata |
| **GANI** | Animation definitions |
| **GSND** | Sound definitions |
| **GSCN** | Scenes and SceneLayers |
| **GSPR** | Sprite definitions |

Studio is an authoring environment, not an embedded game runtime. Game behavior, application structure, and runtime control remain in C#.

See **[Gondwana Studio](Tooling/Gondwana.Tooling.Studio.WinForms)** for details.

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
> Gondwana is actively developed. Its public API is usable today, but breaking changes may occur as the engine and tooling mature.

## Choose Your Workflow

Gondwana does not require one development style. The engine, Studio, standalone tools, CLI, and AI integration are intended to work together rather than replace one another.

### 💻 Code First

Use the .NET templates, NuGet packages, and `gondwana` CLI to build directly in C#.

```console
dotnet new install Gondwana.Templates
dotnet new gondwana-winforms -n MyGame
```

Game behavior remains normal C# code, with direct access to Gondwana's rendering, scene, input, movement, collision, audio, widgets, and hosting APIs.

### 🛠️ Visual Authoring with Gondwana Studio

Use Studio when structured game content benefits from visual authoring.

Studio works directly with Gondwana's first-class definition formats:

```text
GAF   Assets and packaged resources
GTS   Tilesheets
GANI  Animations
GSND  Audio definitions
GSCN  Scenes and SceneLayers
GSPR  Sprites
```

Together, these formats provide first-class definition coverage for the current persistent content categories represented by `EngineState`.

Studio does not maintain simplified copies of the standalone editors. It hosts the same reusable WinForms authoring controls, so improvements to the standalone tools are designed to carry into Studio as well.

### 🤖 ChatGPT and Codex

The official **Gondwana Game Engine** plugin gives ChatGPT and Codex access to the current public Gondwana source, tests, and documentation.

You can ask questions such as:

> Explain how SceneLayer, View, Camera, and Viewport differ.

or give Codex implementation tasks such as:

> Make a simple Pong game using Gondwana and WinForms.

The plugin is designed to ground AI assistance in the current engine instead of relying only on model training data, stale examples, or assumptions from other game engines.

See **[Using Gondwana with ChatGPT and Codex](https://github.com/Isthimius/Gondwana/wiki/Using-Gondwana-with-ChatGPT-and-Codex)**.

> **[SMALL SCREENSHOT PLACEHOLDER — ChatGPT or Codex using the Gondwana Game Engine plugin, preferably showing a concise Gondwana-specific request and source-grounded response.]**

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

---

## 🌐 Repository Mirrors

GitHub is the canonical repository for [Gondwana](https://github.com/Isthimius/Gondwana). Issues, pull requests, discussions, releases, and development activity should be submitted there.

Read-only mirrors are maintained for availability and discoverability:

- **[Bitbucket](https://bitbucket.org/isthimius/gondwana)** — source mirror
- **[Codeberg](https://codeberg.org/Isthimius/Gondwana)** — source mirror
- **[GitLab](https://gitlab.com/isthimius/gondwana)** — source mirror
- **[SourceForge](https://sourceforge.net/projects/gondwana/)** — source mirror and release downloads

---

## 🎯 Who Gondwana Is For

Gondwana is for .NET developers who want to build games in C# rather than surrender the project structure to an editor. It is a good fit when you value:

- Fine-grained control over rendering, timing, input, and movement
- Predictable, debuggable draw and update pipelines
- A code-first workflow without editor lock-in
- Visual content authoring without making the editor the owner of your game architecture
- A reusable foundation for custom 2D and 2.5D games
- Modern .NET architecture grounded in proven rendering principles
- The ability to use AI coding assistants against current engine-specific source and documentation

Gondwana is deliberately an engine and framework, not an all-encompassing visual game-making suite. Gondwana Studio provides first-class visual authoring where it is useful, while game behavior, architecture, and runtime control remain in the developer's C# code.

---

## ✨ Features

- **Cross-platform SkiaSharp rendering** through CPU bitmap and GPU-backed surfaces
- **Backbuffer abstraction** through `BitmapBackbuffer` and `GpuBackbuffer`
- **WinForms, Avalonia, and Blazor adapters**, with ready-to-use hosts for Windows, Linux, macOS, and WebAssembly
- **View-centric layered scenes** with multiple cameras, viewports, parallax, stable z-ordering, and world-space dirty-region tracking
- **Host-owned display effects** for view- and layer-level effects, including fades, slides, directional fills and erases, view shake, and zoom
- **Modular lighting and fog-of-war primitives**, including radial lights, flicker, darkness overlays, and tracked world-space reveal areas
- **Multiple coordinate systems**: orthogonal, rhombic isometric, axial isometric, flat-top hex, pointy-top hex, and oblique
- **Sprites and DirectDrawing** for reusable images, shapes, text, particles, overlays, effects, composites, and high-volume bitmap instances
- **Reusable game UI widgets** with lifecycle events, automatic input registration, focus, keyboard and pointer routing, dragging, hit testing, and components such as `SplashScreen`
- **Sprite and camera movement** with easing, target following, interpolation, scripted paths, and reusable visual effects
- **Collision detection and kinematic resolution**, including per-frame and per-tile collision adjustments for more precise collision geometry
- **High-resolution timing** and thread-safe drawable management with stable z-order sorting
- **Asset support** for tilesheets, sprites, fonts, audio, and packaged Gondwana asset files
- **Unified keyboard, mouse, touch, and gamepad input**, with SDL2 gamepad support available as a dedicated package
- **Audio, MIDI, browser audio, and experimental video integration** through optional packages
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

Gondwana uses a central `Engine` cycle to advance timing, input, movement, animation, and game state. Active `View` instances then project and composite their `SceneLayer` contents through cameras and viewports into a platform backbuffer.

CPU bitmap backbuffers support world-space dirty-region rendering, while GPU-backed surfaces render the full viewport. WinForms, Avalonia, and Blazor adapters handle presentation and native input at the edges, leaving the core engine platform-agnostic.

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

Gondwana's development tools sit above the same public engine APIs and definition models used by applications.

```text
              Authoring / Development
        ┌───────────┬───────────┬──────────────┐
        │           │           │
      C# code     Studio    ChatGPT / Codex
        │           │           │
        └───────────┴─────┬─────┘
                          │
                 Gondwana APIs +
               definition formats
          GAF / GTS / GANI / GSND /
                 GSCN / GSPR
                          │
                          ▼
                     Engine runtime
```

Studio and the standalone tools use the same definition models, serializers, and reusable editor controls. They do not maintain a second Studio-specific representation of Gondwana content.

The AI plugin is similarly additive: it provides current Gondwana-specific context and workflows without changing the runtime architecture or making AI a dependency of a Gondwana game.

### Render resolution and window resizing

`Engine.Instance.Configuration.RenderScale` establishes the logical Backbuffer resolution
(default `1f`; values above one enable supersampling). Window/canvas resize now fits the existing
Backbuffer with centered letterboxing instead of resizing it. Explicitly changing RenderScale
requests a new resolution from the current adapter size. PresentationScale is read-only, and
RenderScalingFilter selects Linear (default) or NearestNeighbor. See the
[viewport scaling migration and validation notes](docs/viewport-scaling.md) for input coordinates,
backend behavior, and Bitmap performance measurements.

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
| [`Gondwana.Video`](https://www.nuget.org/packages/Gondwana.Video) | Experimental video playback through LibVLCSharp |

---

## 🧰 Tooling

Gondwana's tooling is optional. The engine does not require Studio, the standalone authoring tools, the CLI, or the AI plugin at runtime.

| Tool | Install / location | Description |
| --- | --- | --- |
| **Gondwana Studio** | [`Tooling/Gondwana.Tooling.Studio.WinForms`](Tooling/Gondwana.Tooling.Studio.WinForms) | Integrated Windows authoring environment for GAF, GTS, GANI, GSND, GSCN, and GSPR content |
| **Standalone editors** | `Tooling/Gondwana.Tooling.*.WinForms` | Individual authoring utilities for assets, tilesheets, animations, sounds, scenes, and sprites, using the same reusable controls hosted by Studio |
| **Gondwana.Templates** | `dotnet new install Gondwana.Templates` | Project templates for `gondwana-winforms`, `gondwana-avalonia`, and `gondwana-blazor` |
| **Gondwana.Cli** | `dotnet tool install --global Gondwana.Cli` | The `gondwana` CLI for creating projects, checking an environment with `gondwana doctor`, and packing or inspecting asset files |
| **Gondwana Game Engine plugin** | ChatGPT / Codex Plugin Directory | Engine-aware AI assistance using the current public Gondwana source, tests, and wiki |
| **Gondwana.Mcp** | [`Tooling/Gondwana.Mcp`](Tooling/Gondwana.Mcp) | Read-only MCP service that powers the official Gondwana AI integration |

---

## 🧭 Key Design Principles

- **Code first**: Game code owns behavior and can reach the rendering and update pipeline directly.
- **Tooling is additive**: Studio and the standalone editors author structured content without taking ownership of application architecture.
- **World space first**: Gameplay and scene logic operate in world pixels; views and cameras convert world coordinates to screen coordinates at render time.
- **Dirty-region rendering where it pays**: CPU bitmap backbuffers redraw changed world-space regions rather than repainting the entire frame.
- **Layered, view-centric scenes**: Scenes contain independently rendered `SceneLayer` instances, while `View` and `ViewRenderer` make multiple cameras, viewports, and split views natural.
- **Adapters at the edges**: Platform projects host render surfaces and wire native input while the core remains platform-agnostic.
- **Explicit composition**: Sprites, direct drawings, composites, widgets, and scene layers have clear ownership and ordering rules.
- **Predictable behavior**: Stable ordering and explicit timing make rendering and movement easier to debug.
- **Open formats**: GAF, GTS, GANI, GSND, GSCN, and GSPR are explicit engine content formats rather than opaque Studio-owned project blobs.
- **AI is optional and grounded**: The official ChatGPT/Codex plugin can inspect current public Gondwana material without becoming a runtime dependency.
- **Modularity without ceremony**: Hosting, widgets, platform adapters, audio, input, video, tooling, and AI integration remain separate so applications take only what they need.

---

## 🛠 Roadmap

_Gondwana is actively evolving, with an emphasis on strengthening the engine, authoring workflow, and runtime systems._

* [x] WebAssembly support through Blazor
* [x] Integrated Gondwana Studio authoring environment
* [x] First-class GAF, GTS, GANI, GSND, GSCN, and GSPR authoring
* [x] Full platformer sample
* [x] WebGL-backed Blazor rendering adapter
* [ ] TMX import / tile-map interchange tooling
* [ ] Expanded 2D physics, including momentum, elasticity, and additional collision shapes
* [ ] Native, first-class pathfinding
* [ ] Initial client/server networking support
* [ ] Android and iOS support via .NET MAUI adapters

---

## 🤝 Contributing

Contributions are welcome.

- Open an issue for a bug report or feature request.
- Fork the repository, create a focused branch, and submit a pull request.

---

## 📜 License

Gondwana is available under the [MIT License](LICENSE).

**Third-party libraries**  
Gondwana uses **[Skia](https://skia.org/)** (© Google), licensed under the [BSD 3-Clause License](https://opensource.org/license/bsd-3-clause), through **[SkiaSharp](https://github.com/mono/SkiaSharp)** (© Microsoft and contributors), licensed under the [MIT License](https://opensource.org/license/mit).

---

## ☕ Support Gondwana

Gondwana is developed and maintained independently. If you find the engine useful, consider [buying me a coffee](https://www.buymeacoffee.com/mikeleeisback) to support its continued development.
