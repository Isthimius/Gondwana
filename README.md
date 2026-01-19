# Gondwana Game Engine
<img src="https://github.com/user-attachments/assets/64372678-7d38-47f8-b01a-511c3ef407cc"
     alt="Gondwana" align="left" width="40%" />

**Gondwana** is a cross-platform 2.5D game and rendering engine written in C#/.NET 8. It provides fine-grained control over rendering, timing, and scene composition, with built-in support for parallax, z-ordering, pixel overhang, collision detection, and particle effects. Gondwana targets desktop, mobile, and web platforms using SkiaSharp for graphics and NAudio for sound.

Rather than hiding the render pipeline behind an editor, Gondwana embraces a code-first, engine-driven design. Developers can own their game loop and rendering flow end-to-end when needed, while still benefiting from sensible defaults that allow simpler games, simulations, and tools to come together quickly. This approach modernizes classic Win32/GDI-era rendering patterns into a clean, modular architecture with explicit control over draw order, dirty-region updates, and timing—yielding a predictable, debuggable engine that works out of the box but does not get in the way as projects grow.

- 📘 **Engine Architecture & Guides** — [GitHub Wiki](https://github.com/Isthimius/Gondwana/wiki)
- 📚 **API Reference (Doxygen)** — [https://isthimius.github.io/Gondwana/](https://isthimius.github.io/Gondwana/)

---

## 🎯 Who Gondwana Is For

Gondwana is designed for developers who want:
- Fine-grained control over rendering and timing
- Deterministic, debuggable draw pipelines
- A code-first engine without editor lock-in
- A modern .NET engine that respects classic rendering principles

It is intended to serve as a flexible foundation for custom 2D and 2.5D games, tools, and simulations.

---

## ✨ Features

- **Cross-platform rendering** via SkiaSharp (`SKSurface`, `SKBitmap` backbuffers)  
- **Backbuffer abstraction** (`BitmapBackbuffer`, `GpuBackbuffer`) for multiple platforms  
- **DirectDrawing system** for sprites, shapes, text, and effects:
  - `DirectRectangle`, `DirectImage`, `TextBlock`, `DirectParticles` (new particle system with emitters)  
- **High-resolution timing** (`HighResTimer`) for smooth frame updates  
- **Thread-safe rendering manager** (`DirectDrawingManager`) with Z-order sorting  
- **Extensible resource pipeline** for tilesheets, sprites, and audio  
- **Experimental video & audio integration** (`LibVLCSharp`, `NAudio`)

---

## 📂 Project Structure

At runtime, Gondwana is driven by a central `Engine` loop responsible for advancing time, polling platform-specific input, updating game state, and rendering only what has changed. The engine is built around a world-space, view-centric rendering model designed to minimize redraw work while supporting multiple cameras and scene layers.

### Runtime Flow

Each engine cycle proceeds through the following stages:

1. **World-space change tracking**  
   Any state changes enqueue world-space dirty regions into the owning `SceneLayer`’s `RefreshQueue`. This allows the engine to track *what* changed and *where*, without relying on full-frame redraws.

2. **View-based rendering**  
   During rendering, the `ViewRenderer` iterates active `View` instances in deterministic Z-order. Each view applies its camera and viewport transforms, then asks visible scene layers to redraw only the affected regions into a backbuffer.

3. **Composition and presentation**  
   Sprites and tiles are drawn from cached tilesheets, animations advance frame-by-frame, and the composed backbuffer is finally presented by the platform host (WinForms, Web, etc.).

4. **Timers and input polling**  
   High-resolution timers advance simulation time while input adapters poll keyboard, mouse, and gamepad state. These events may move sprites, advance animations, or otherwise modify scene state.

This dirty-region, view-centric design allows Gondwana to efficiently render complex scenes with multiple layers and cameras while keeping the core engine logic platform-agnostic. By separating world updates from presentation concerns, the engine remains predictable, debuggable, and scalable as projects grow.

### Core Namespaces (high level)

| Namespace                   | Responsibility                                                                                 |
| --------------------------- | ---------------------------------------------------------------------------------------------- |
| **Gondwana**                | Core engine loop, lifecycle management, configuration, and global services.                    |
| **Gondwana.Drawing**        | Low-level drawing primitives, sprites, tilesheets, animation, particles, and direct drawables. |
| **Gondwana.Input**          | Unified input polling for keyboard, mouse, and gamepad devices.                                |
| **Gondwana.Movement**       | Sprite movement controllers, easing functions, and scripted motion paths.                      |
| **Gondwana.Rendering**      | Backbuffer abstractions, view rendering, cameras, and platform-agnostic draw flow.             |
| **Gondwana.Scenes**         | Scene and SceneLayer composition, visibility, and layer-level refresh tracking.                |
| **Gondwana.Timers**         | High-resolution timing, scheduled callbacks, and engine-cycle events.                          |
| **Gondwana.Audio / Video**  | Audio playback, mixing, MIDI support, and experimental video integration.                      |
| **Gondwana.WinForms / Web** | Platform adapters responsible for hosting render surfaces and wiring input.                    |


---

## 🧭 Key Design Principles
- **Dirty-region rendering (`RefreshQueue`)**: The engine tracks what changed and redraws only those world-space regions, instead of repainting the whole screen every frame.
- **World-space first**: The engine reasons in world pixels; views/cameras/viewport transforms convert world → screen at render time. This keeps logic consistent and avoids “screen math” leaking into gameplay code.
- **Layered scenes**: A Scene is composed of SceneLayers (often with parallax). Each layer maintains its own refresh tracking and draw path.
- **View-centric rendering**: Rendering flows through View / ViewRenderer so multiple cameras/viewports (or multiplayer split views) are natural, not bolted on.
- **Adapters at the edges**: Platform projects (WinForms/Web) host the render surface and input wiring, while the core engine stays platform-agnostic.
- **Deterministic ordering**: Where ordering matters (views, layers, drawables), the engine uses stable sort rules so rendering remains predictable and debuggable.

---

## 📦 Prerequisites

The Gondwana Core library depends on the following NuGet packages:

- **Microsoft.Extensions.Configuration** (10.0.1)  
- **Microsoft.Extensions.Configuration.Binder** (10.0.1)  
- **Microsoft.Extensions.Configuration.Json** (10.0.1)  
- **Microsoft.Extensions.Logging.Console** (10.0.1)  
- **Microsoft.Extensions.Logging.Debug** (10.0.1)  
- **NAudio** (2.2.1) — audio playback and mixing  
- **Newtonsoft.Json** (13.0.4) — JSON serialization  
- **SharpZipLib** (1.4.2) — archive and compression support  
- **SkiaSharp** (3.119.1) — 2D rendering backend  
- **SkiaSharp.HarfBuzz** (3.119.1) — advanced text shaping/rendering


## 🔧 Development & Build Tooling

The following packages are required only when building Gondwana from source:

- Nerdbank.GitVersioning (3.9.50) — deterministic versioning based on Git history

---

## 🛠 Roadmap

_Gondwana is actively evolving, with a focus on strengthening core simulation and tooling rather than chasing engine sprawl._

- [ ] Physics integration (collisions, rigid bodies)  
- [ ] Scene system for complex game flow  
- [ ] More samples: tile maps, platformer demo  
- [ ] Improved WebAssembly support  

---

## 🤝 Contributing

Contributions are welcome!  
- Open an issue for bugs or feature requests.  
- Fork, branch, and PR to contribute code.  

---

## 📜 License

MIT License — see [LICENSE](LICENSE) for details.  

**Third-Party Libraries** <br />
Gondwana uses **Skia** (© Google) via **SkiaSharp** (© Microsoft and contributors), licensed under the BSD 3-Clause license.
