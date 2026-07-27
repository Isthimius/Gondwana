# Gondwana Game Engine

[![NuGet](https://img.shields.io/nuget/v/Gondwana)](https://www.nuget.org/packages/Gondwana)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Gondwana)](https://www.nuget.org/packages/Gondwana)
[![License](https://img.shields.io/github/license/Isthimius/Gondwana)](https://github.com/Isthimius/Gondwana/blob/master/LICENSE)
[![Docs](https://img.shields.io/badge/docs-wiki-blue)](https://github.com/Isthimius/Gondwana/wiki)
[![API](https://img.shields.io/badge/api-reference-blue)](https://isthimius.github.io/Gondwana/)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Platforms](https://img.shields.io/badge/platforms-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20WebAssembly-blue)

<img alt="Gondwana logo" src="https://github.com/user-attachments/assets/cefd03d0-de2b-474e-8f72-e4ab672cede3" align="left" width="40%" />

**Gondwana** is a cross-platform 2D and 2.5D game and rendering engine written in C# and .NET 8. It provides fine-grained control over rendering, timing, movement, input, collision detection, and scene composition, with built-in support for layered worlds, multiple views, parallax, z-ordering, pixel overhang, particles, and several grid projections.

Gondwana targets Windows, Linux, macOS, and WebAssembly through SkiaSharp-based rendering, with dedicated integrations for WinForms, Avalonia, and Blazor. Optional packages add desktop and browser audio, MIDI playback, SDL2 gamepad input, video playback, hosting, and reusable game UI widgets.

Rather than hiding the render pipeline behind an editor, Gondwana embraces a code-first, engine-driven design. Developers retain direct ownership of the game loop and rendering flow when needed, while sensible defaults and ready-to-use hosts keep smaller games approachable.

The engine carries forward the predictability of classic Win32/GDI-era rendering—explicit draw order, dirty-region updates, scene composition, and timing—inside a modern, modular architecture. The result is an engine intended to be understandable, debuggable, and useful without demanding that a project surrender control as it grows.

## Get Started

**[Make Your First Game in 1 Hour with Gondwana](https://github.com/Isthimius/Gondwana/wiki/Make-Your-First-Game-in-1-Hour)**

## Documentation & Resources

- 📘 **[Engine Architecture & Guides](https://github.com/Isthimius/Gondwana/wiki)**
- 📚 **[API Reference](https://isthimius.github.io/Gondwana/)**
- 📦 **[NuGet Package](https://www.nuget.org/packages/Gondwana)**
- 🏷️ **[GitHub Releases](https://github.com/Isthimius/Gondwana/releases)**
- 📜 **[Release History](https://github.com/Isthimius/Gondwana/blob/master/CHANGELOG.md)**
- ⬇️ **[Latest CI Build](https://github.com/Isthimius/Gondwana/actions/workflows/ci-master.yml)**

---

## 🎬 Demo Previews

**Spot!**

<img width="45%" alt="Spot gameplay preview 1" src="https://github.com/user-attachments/assets/c29ddd87-fb82-46dc-ad5e-6388c11ba50d" /> <img width="45%" alt="Spot gameplay preview 2" src="https://github.com/user-attachments/assets/0aef0b63-1c16-44be-b6a6-d456f4799ce8" />

**Particle Test**  
<img width="800" height="388" alt="Gondwana particle-system demonstration" src="https://github.com/user-attachments/assets/105740af-e8e5-4f92-92e2-7986612008a1" />

**Coordinates Test**  
<img width="800" height="447" alt="Gondwana coordinate-system demonstration" src="https://github.com/user-attachments/assets/6ae8183e-b4e6-4740-9a01-9679ed66cd40" />

---

## 🎯 Who Gondwana Is For

Gondwana is for .NET developers who want to build games in C# rather than assemble them entirely through an editor. It is a good fit when you value:

- Fine-grained control over rendering, timing, input, and movement
- Deterministic, debuggable draw and update pipelines
- A code-first workflow without editor lock-in
- A reusable foundation for custom 2D and 2.5D games
- Modern .NET architecture grounded in proven rendering principles

Gondwana is deliberately an engine and framework, not an all-encompassing visual game-making suite. Its tooling supports the code-first workflow rather than replacing it.

---

## ✨ Features

- **Cross-platform rendering** through SkiaSharp using CPU bitmap and GPU-backed surfaces
- **Backbuffer abstraction** through `BitmapBackbuffer` and `GpuBackbuffer`, including GPU FPS tracking
- **Platform adapters for WinForms, Avalonia, and Blazor**:
  - WinForms for Windows
  - Avalonia for Windows, macOS, and Linux
  - Blazor for WebAssembly
- **Structured host lifecycle** through `GameHostBase` and ready-to-use WinForms, Avalonia, and Blazor hosts
- **View-centric layered scenes** with multiple cameras, viewports, parallax, stable z-ordering, and world-space dirty-region tracking
- **Multiple coordinate systems**: orthogonal, rhombic isometric, axial isometric, flat-top hex, pointy-top hex, and oblique
- **DirectDrawing system** for sprites, shapes, text, overlays, and effects:
  - `DirectRectangle`, `DirectImage`, `TextBlock`, and `DirectParticles`
  - `ImageInstanceLayer` for efficient rendering of many reusable, movable bitmap instances
  - `DirectComposite` for grouped drawing elements with consistent view or scene-layer ownership
- **Reusable game UI widgets** with lifecycle events, automatic input registration, focus, keyboard and pointer routing, dragging, hit testing, and components such as `SplashScreen`
- **Sprite and camera movement** with easing-based tweening, target following, smooth interpolation, and scripted motion paths
- **Sprite effects** including visual jiggle and pulsing or looping resize behaviors with completion events
- **Collision detection and kinematic resolution**, including per-frame and per-tile collision adjustments for more precise collision geometry
- **High-resolution timing** through `HighResTimer`
- **Thread-safe drawable management** with deterministic z-order sorting
- **Asset support** for tilesheets, sprites, fonts, audio, and packaged Gondwana asset files
- **Centralized font management** through the font asset type and `FontManager`
- **Unified keyboard, mouse, touch, and gamepad input**, with SDL2 gamepad support available as a dedicated package
- **Audio, MIDI, browser audio, and experimental video integration** through optional packages

---

## 📂 Architecture

At runtime, Gondwana is driven by a central `Engine` loop responsible for advancing time, polling input, updating engine state, and rendering changed content. The engine uses a world-space, view-centric rendering model designed to minimize redraw work while supporting multiple cameras and scene layers.

### Runtime Flow

Each engine cycle proceeds through four broad stages:

1. **Update and input**  
   High-resolution timers advance engine time while platform adapters poll keyboard, pointer, touch, and gamepad state. Movement controllers, widgets, animations, and game code can update world state in response.

2. **World-space change tracking**  
   On CPU bitmap backbuffers, state changes enqueue world-space dirty regions in the owning `SceneLayer`'s `RefreshQueue`. The engine therefore knows both *what* changed and *where* without requiring a full-frame repaint.

3. **View-based rendering**  
   `ViewRenderer` processes active `View` instances in deterministic z-order. Each view applies its camera and viewport transforms and renders its visible scene layers. CPU-backed surfaces redraw affected regions; GPU-backed surfaces use a full-viewport path designed around predictable GPU synchronization.

4. **Composition and presentation**  
   Tiles, sprites, direct drawings, particles, widgets, and other content are composed into the backbuffer, which the platform host then presents.

Separating world updates from platform presentation keeps the core engine platform-agnostic and makes rendering behavior easier to inspect, reason about, and extend.

### Major Namespaces and Assemblies

| Namespace / assembly | Responsibility |
| --- | --- |
| **Gondwana** | Engine loop, lifecycle, configuration, shared services, and core abstractions |
| **Gondwana.Assets** | Asset identifiers, asset types, packaged asset files, and loading |
| **Gondwana.Audio** | Platform-agnostic audio resources and playback management |
| **Gondwana.Configuration** | Engine configuration models and configuration-file loading |
| **Gondwana.Drawing** | Sprites, tilesheets, animation, particles, text, shapes, composites, and direct drawing |
| **Gondwana.Drawing.Coordinates** | Orthogonal, isometric, hexagonal, and oblique coordinate transforms |
| **Gondwana.Input** | Keyboard, mouse, touch, gesture, and gamepad input |
| **Gondwana.Logging** | Engine logging and logging configuration |
| **Gondwana.Movement** | Movement controllers, easing, following, interpolation, and scripted paths |
| **Gondwana.Physics.Collisions** | Collision detection, collision geometry, and kinematic resolution |
| **Gondwana.Rendering** | Backbuffers, cameras, views, render surfaces, and platform-agnostic draw flow |
| **Gondwana.Scenes** | Scenes, scene layers, spatial organization, and refresh tracking |
| **Gondwana.Timers** | High-resolution timing, scheduled callbacks, and engine-cycle events |
| **Gondwana.Hosting** | Cross-platform `GameHostBase` lifecycle |
| **Gondwana.Widgets** | Reusable game UI, widget lifecycle, focus, input routing, and interaction |
| **Gondwana.WinForms** | Windows render-surface and input adapters |
| **Gondwana.Avalonia** | Windows, macOS, and Linux render-surface and input adapters |
| **Gondwana.Blazor** | WebAssembly rendering, input, browser integration, and components |

---

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
| [`Gondwana.Audio.Midi`](https://www.nuget.org/packages/Gondwana.Audio.Midi) | MIDI playback and SoundFont support |
| [`Gondwana.Audio.Browser`](https://www.nuget.org/packages/Gondwana.Audio.Browser) | Browser and WebAssembly audio through the HTML5 Audio API and JavaScript interop |
| [`Gondwana.Video`](https://www.nuget.org/packages/Gondwana.Video) | Experimental video playback through LibVLCSharp |

---

## 🧰 Tooling

| Tool | Install / location | Description |
| --- | --- | --- |
| **Gondwana.Templates** | `dotnet new install Gondwana.Templates` | Project templates for `gondwana-winforms`, `gondwana-avalonia`, and `gondwana-blazor` |
| **Gondwana.Cli** | `dotnet tool install --global Gondwana.Cli` | The `gondwana` CLI for creating projects, checking an environment with `gondwana doctor`, and packing or inspecting asset files |
| **Gondwana.Studio** | Build from `Tooling/Gondwana.Studio` | Cross-platform Avalonia front end for Gondwana project and asset tooling |
| **Gondwana.Studio.WinForms** | Build from `Tooling/Gondwana.Studio.WinForms` | Windows-native Studio front end sharing framework-agnostic editor logic with the Avalonia application |

---

## 🧭 Key Design Principles

- **Code first**: Game code owns behavior and can reach the rendering and update pipeline directly.
- **World space first**: Gameplay and scene logic operate in world pixels; views and cameras convert world coordinates to screen coordinates at render time.
- **Dirty-region rendering where it pays**: CPU bitmap backbuffers redraw changed world-space regions rather than repainting the entire frame.
- **Layered, view-centric scenes**: Scenes contain independently rendered `SceneLayer` instances, while `View` and `ViewRenderer` make multiple cameras, viewports, and split views natural.
- **Adapters at the edges**: Platform projects host render surfaces and wire native input while the core remains platform-agnostic.
- **Explicit composition**: Sprites, direct drawings, composites, widgets, and scene layers have clear ownership and ordering rules.
- **Deterministic behavior**: Stable ordering and explicit timing make rendering and movement easier to debug.
- **Modularity without ceremony**: Hosting, widgets, platform adapters, audio, input, and video remain separate packages so applications take only what they need.

---

## 🛠 Roadmap

_Gondwana is actively evolving, with an emphasis on strengthening the engine and its tooling rather than chasing feature-list sprawl._

- [ ] Supplemental level- and asset-design tooling
- [ ] Additional samples, including tile-map and platformer demonstrations
- [x] WebAssembly support through Blazor
- [ ] TMX tile-map support
- [ ] Native, first-class pathfinding
- [ ] Further rendering-pipeline extensibility
- [ ] Initial client/server networking support

---

## 🤝 Contributing

Contributions are welcome.

- Open an issue for a bug report or feature request.
- Fork the repository, create a focused branch, and submit a pull request.

---

## 📜 License

Gondwana is available under the [MIT License](LICENSE).

**Third-party libraries**  
Gondwana uses **Skia** (© Google) through **SkiaSharp** (© Microsoft and contributors), licensed under the BSD 3-Clause license.
