# Gondwana Game Engine

[![NuGet](https://img.shields.io/nuget/v/Gondwana)](https://www.nuget.org/packages/Gondwana)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Gondwana)](https://www.nuget.org/packages/Gondwana)
[![License](https://img.shields.io/github/license/Isthimius/Gondwana)](https://github.com/Isthimius/Gondwana/blob/master/LICENSE)
[![Docs](https://img.shields.io/badge/docs-wiki-blue)](https://github.com/Isthimius/Gondwana/wiki)
[![API](https://img.shields.io/badge/api-reference-blue)](https://isthimius.github.io/Gondwana/)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Platforms](https://img.shields.io/badge/platforms-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20WebAssembly-blue)

<img alt="Gondwana logo" src="https://github.com/user-attachments/assets/cefd03d0-de2b-474e-8f72-e4ab672cede3" align="left" width="40%" />

**Gondwana** is a code-first, cross-platform 2D and 2.5D game and rendering engine for C# and .NET 8. It gives developers fine-grained control over rendering, timing, movement, input, collision detection, and scene composition.

Gondwana targets Windows, Linux, macOS, and WebAssembly through SkiaSharp-based rendering, with dedicated integrations for WinForms, Avalonia, and Blazor. Its layered worlds support multiple views, parallax, stable z-ordering, particles, pixel overhang, and several grid projections. Optional packages add desktop and browser audio, MIDI playback, SDL2 gamepad input, video playback, hosting, and reusable game UI widgets.

Rather than hiding the render pipeline behind an editor, Gondwana embraces a code-first, engine-driven design. Developers retain direct ownership of the game loop and rendering flow when needed, while sensible defaults and ready-to-use hosts keep smaller games approachable.

The engine carries forward the predictability of classic Win32/GDI-era rendering—explicit draw order, dirty-region updates where appropriate, scene composition, and timing—inside a modern, modular architecture. The result is an engine intended to remain understandable and debuggable without demanding that a project surrender control as it grows.

<br clear="left" />

## 🎮 Gondwana in Action

### [Spot!](Demos/Spot)

Spot! is Gondwana's primary playable showcase.

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

Prefer an AI-assisted workflow? See **[Using Gondwana with ChatGPT and Codex](https://github.com/Isthimius/Gondwana/wiki/Using-Gondwana-with-ChatGPT-and-Codex)**.

> [!NOTE]
> Gondwana is actively developed. Its public API is usable today, but breaking changes may occur as the engine and tooling mature.

## Documentation & Resources

- 📘 **[Engine Wiki](https://github.com/Isthimius/Gondwana/wiki)**
- 📚 **[API Reference](https://isthimius.github.io/Gondwana/)**
- 📦 **[NuGet Package](https://www.nuget.org/packages/Gondwana)**
- 🏷️ **[GitHub Releases](https://github.com/Isthimius/Gondwana/releases)**
- 📜 **[Release History](https://github.com/Isthimius/Gondwana/blob/master/CHANGELOG.md)**
- ✅ **[Latest CI Run](https://github.com/Isthimius/Gondwana/actions/workflows/ci-master.yml)**
- 💬 **[Discussions](https://github.com/Isthimius/Gondwana/discussions)**

---

## 🎯 Who Gondwana Is For

Gondwana is for .NET developers who want to build games in C# rather than assemble them entirely through an editor. It is a good fit when you value:

- Fine-grained control over rendering, timing, input, and movement
- Predictable, debuggable draw and update pipelines
- A code-first workflow without editor lock-in
- A reusable foundation for custom 2D and 2.5D games
- Modern .NET architecture grounded in proven rendering principles

Gondwana is deliberately an engine and framework, not an all-encompassing visual game-making suite. Its tooling supports the code-first workflow rather than replacing it.

---

## ✨ Features

- **Cross-platform SkiaSharp rendering** through CPU bitmap and GPU-backed surfaces
- **Backbuffer abstraction** through `BitmapBackbuffer` and `GpuBackbuffer`
- **WinForms, Avalonia, and Blazor adapters**, with ready-to-use hosts for Windows, Linux, macOS, and WebAssembly
- **View-centric layered scenes** with multiple cameras, viewports, parallax, stable z-ordering, and world-space dirty-region tracking
- **Multiple coordinate systems**: orthogonal, rhombic isometric, axial isometric, flat-top hex, pointy-top hex, and oblique
- **Sprites and DirectDrawing** for reusable images, shapes, text, particles, overlays, effects, composites, and high-volume bitmap instances
- **Reusable game UI widgets** with lifecycle events, automatic input registration, focus, keyboard and pointer routing, dragging, hit testing, and components such as `SplashScreen`
- **Sprite and camera movement** with easing, target following, interpolation, scripted paths, and reusable visual effects
- **Collision detection and kinematic resolution**, including per-frame and per-tile collision adjustments for more precise collision geometry
- **High-resolution timing** and thread-safe drawable management with stable z-order sorting
- **Asset support** for tilesheets, sprites, fonts, audio, and packaged Gondwana asset files
- **Unified keyboard, mouse, touch, and gamepad input**, with SDL2 gamepad support available as a dedicated package
- **Audio, MIDI, browser audio, and experimental video integration** through optional packages

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

At runtime, a central `Engine` loop advances timing, input, movement, animation, and game state before rendering active `View` instances into a platform backbuffer. CPU bitmap backbuffers can redraw only changed world-space regions, while GPU-backed surfaces use a full-viewport path. Platform adapters handle presentation and native input at the edges, keeping the core engine platform-agnostic.

See the **[Engine Wiki](https://github.com/Isthimius/Gondwana/wiki)** for architecture guides, rendering-pipeline documentation, coordinate-space references, and subsystem walkthroughs.

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
| **Gondwana.Tooling.Studio.Avalonia** | Build from `Tooling/Gondwana.Tooling.Studio.Avalonia` | Cross-platform Avalonia front end for Gondwana project and asset tooling |
| **Gondwana.Tooling.Studio.WinForms** | Build from `Tooling/Gondwana.Tooling.Studio.WinForms` | Windows-native Studio front end sharing framework-agnostic editor logic with the Avalonia application |

---

## 🧭 Key Design Principles

- **Code first**: Game code owns behavior and can reach the rendering and update pipeline directly.
- **World space first**: Gameplay and scene logic operate in world pixels; views and cameras convert world coordinates to screen coordinates at render time.
- **Dirty-region rendering where it pays**: CPU bitmap backbuffers redraw changed world-space regions rather than repainting the entire frame.
- **Layered, view-centric scenes**: Scenes contain independently rendered `SceneLayer` instances, while `View` and `ViewRenderer` make multiple cameras, viewports, and split views natural.
- **Adapters at the edges**: Platform projects host render surfaces and wire native input while the core remains platform-agnostic.
- **Explicit composition**: Sprites, direct drawings, composites, widgets, and scene layers have clear ownership and ordering rules.
- **Predictable behavior**: Stable ordering and explicit timing make rendering and movement easier to debug.
- **Modularity without ceremony**: Hosting, widgets, platform adapters, audio, input, and video remain separate packages so applications take only what they need.

---

## 🛠 Roadmap

_Gondwana is actively evolving, with an emphasis on strengthening the engine and its tooling._

* [x] WebAssembly support through Blazor
* [ ] Tilesheet and SceneLayer tooling, including TMX support
* [ ] Full platformer sample
* [ ] WebGL-backed Blazor rendering adapter
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
Gondwana uses **Skia** (© Google) through **SkiaSharp** (© Microsoft and contributors), licensed under the BSD 3-Clause license.
