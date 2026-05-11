# What Is Gondwana?

Gondwana is a cross-platform 2.5D game and rendering engine written in C#/.NET 8.  
It targets desktop (Windows, Linux, macOS), with experimental mobile and web support.

It is a **code-first engine** — no editor, no scene GUI — developers own the game loop and rendering pipeline directly.

---

# Key Technologies

| Technology | Role |
|-----------|------|
| .NET 8 / C# | Primary language and runtime |
| SkiaSharp | Cross-platform 2D rendering (GPU + bitmap backbuffers) |
| NAudio | Audio playback and mixing |
| LibVLCSharp | Experimental video playback |
| SDL2 | Optional gamepad input (via Gondwana.Input.SDL2) |
| WinForms | Desktop host adapter (Windows/Linux via Mono) |
| Nerdbank.GitVersioning | Deterministic versioning from `version.json` |

---

# Solution Structure

The solution (`Gondwana.sln`) is composed of multiple focused projects.

---

## Core Library

### `Gondwana/`
The main engine library. Contains:

- **Engine.cs** — Central game loop (timing, input polling, scene updates, rendering)
- **EngineManagers.cs, EngineDispatcher.cs** — Lifecycle and dispatch

### Rendering
- Backbuffers (`BitmapBackbuffer`, `GpuBackbuffer`)
- `RefreshQueue`
- `RenderSurfaceHost`
- Views / Camera / Viewport

### Scenes
- `Scene`
- `SceneLayer`
- `SceneLayerTile`
- Hierarchical scene graph

### Drawing
- Sprites
- Tilesheets
- Animations
- Collision shapes
- Direct drawables:
  - `DirectImage`
  - `DirectRectangle`
  - `TextBlock`
  - `DirectParticles`
- Coordinates, overhang, tiles

### Input
- Keyboard, mouse, and gamepad polling abstractions

### Movement
- `MovementController`
  - Follow
  - Integrated
  - Scripted modes
- Easing functions

### Audio
- `AudioResourceManager`
- `PlatformAudioFactory`
- Stereo panning

### Timers
- High-resolution timing
- Scheduled callbacks

### Collisions
- Bounding-volume detection
- Kinematic physics

### Supporting Infrastructure
- Configuration
- Extensibility
- Logging
- SkiaSharp integration

---

## Platform Adapters

- **Gondwana.Hosting/**
  - `GameHostBase` — base class for platform hosts

- **Gondwana.WinForms/**
  - WinForms-specific rendering surface
  - Input wiring
  - Audio integration

- **Gondwana.WinForms.Hosting/**
  - WinForms hosting glue

---

## Optional Add-on Libraries

- **Gondwana.Audio.Midi/**
  - MIDI file reading and synthesis (bundled `.sf2` soundfont)

- **Gondwana.Input.SDL2/**
  - SDL2-based gamepad input provider

- **Gondwana.Video/**
  - Video playback via LibVLCSharp (`VlcVideoPlayer`)

---

## Demos

- `Demos/Gondwana.CoordinateTest` — Coordinate system test demo  
- `Demos/Gondwana.ParticleTest` — Particle system demo  
- `Demos/Slider`, `Demos/Spot` — Additional sample games/demos  

---

# Runtime Flow (How It Works)

Each engine cycle:

1. **Dirty-region tracking**  
   State changes enqueue world-space dirty rectangles into the affected `SceneLayer`'s `RefreshQueue`  
   *(no full-frame redraws)*

2. **View rendering**  
   `ViewRenderer` iterates active `View` instances in Z-order  
   Each applies its Camera/Viewport transform and redraws only changed regions into a backbuffer

3. **Composition & presentation**  
   - Sprites and tiles drawn from cached tilesheets  
   - Animations advance  
   - Final backbuffer presented by the platform host

4. **Timers & input**  
   - High-resolution timers advance simulation time  
   - Input adapters poll keyboard, mouse, and gamepad  
   - Scene state is updated accordingly  

---

# Key Design Principles

- **World-space first**  
  All logic uses world coordinates; camera/viewport transforms occur only at render time

- **Dirty-region rendering**  
  Only changed regions are redrawn, not the full screen

- **Layered scenes with parallax**  
  A `Scene` contains multiple `SceneLayers`, each with independent refresh tracking and parallax settings

- **View-centric rendering**  
  Multiple cameras/viewports are first-class (split-screen, HUD layers)

- **Platform adapters at the edges**  
  Core engine is platform-agnostic; adapters live in separate projects

- **Deterministic ordering**  
  Stable sorting for Z-order, layers, and drawables ensures predictable rendering

---

# Versioning

The project uses **Nerdbank.GitVersioning**.

- Canonical version is defined in `version.json`
- Automatically applied via `Directory.Build.props`
- Do **not** hard-code `<Version>` or `<FileVersion>` in `.csproj` files
---

## Where to Go Next

- New here? → [[Make Your First Game in 15 Minutes]]
- Want the mental model? → [[Engine Architecture Overview]]
