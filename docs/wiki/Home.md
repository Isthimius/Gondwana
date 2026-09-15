# Gondwana Game Engine

> A code-first 2D and 2.5D game engine for C# and .NET 8, targeting Windows, Linux, macOS, and WebAssembly.

Gondwana combines modern .NET with an explicit, predictable rendering model inspired by classic game and graphics programming. It provides first-class scenes, layers, cameras, viewports, sprites, tiles, movement, collisions, input, audio, particles, widgets, and platform hosting; without requiring developers to surrender control to an editor-driven workflow.

**Start here:** [[Make Your First Game in 30 Minutes]]  
**Architecture:** [[Engine Architecture Overview]]  
**API reference:** https://isthimius.github.io/Gondwana/  
**NuGet:** https://www.nuget.org/packages/Gondwana  
**Source:** https://github.com/Isthimius/Gondwana

---

## Contents

- [What Is Gondwana?](#what-is-gondwana)
- [Where Gondwana Fits](#where-gondwana-fits)
- [Supported Platforms](#supported-platforms)
- [Core Capabilities](#core-capabilities)
- [Architecture at a Glance](#architecture-at-a-glance)
- [Solution Structure](#solution-structure)
- [Runtime Cycle](#runtime-cycle)
- [Rendering Model](#rendering-model)
- [Coordinate Systems](#coordinate-systems)
- [Packages and Tooling](#packages-and-tooling)
- [Key Design Principles](#key-design-principles)
- [Versioning](#versioning)
- [Where to Go Next](#where-to-go-next)

---

# What Is Gondwana?

Gondwana is a cross-platform 2D and 2.5D game and rendering engine written in C# for .NET 8.

It is designed primarily for developers who want to build games in code while retaining direct control over engine lifecycle, scene composition, rendering, timing, input, and game state. Gondwana supplies the runtime and the engine primitives; the game remains ordinary C# code.

Gondwana is **editor-optional**, not editor-dependent. The engine does not require a scene GUI, proprietary project format, or visual scripting environment. Supplemental tools such as Gondwana Studio, the Gondwana CLI, and project templates can assist development without becoming mandatory runtime dependencies.

Gondwana is also **not a traditional Entity Component System**. Its core model is intentionally object-oriented:

- A `Scene` contains ordered `SceneLayer` instances.
- Layers contain tiles, sprites, and other scene content.
- `View` and Camera objects determine how scenes are observed.
- DirectDrawing objects provide view-bound or world-bound drawing primitives.
- Movement, collisions, input, audio, timers, widgets, and other behaviors are exposed through focused engine subsystems.

Games may build their own entity or component model on top of these primitives, but Gondwana does not force every project into a universal ECS abstraction.

---

# Where Gondwana Fits

| Gondwana is | Gondwana is not |
|---|---|
| A code-first game and rendering engine | A full 3D engine |
| A reusable runtime for 2D and 2.5D games | An editor-first content-authoring environment |
| A scene-, layer-, sprite-, and view-oriented architecture | A mandatory Entity Component System |
| A platform-neutral core with explicit host adapters | A single-platform UI framework |
| A practical foundation for tile-based and sprite-based worlds | A black box that hides the render pipeline |
| Suitable for custom engines, tools, demos, and games | A replacement for game-specific architecture |

Gondwana is a natural fit for developers who value deterministic behavior, inspectable code, explicit lifecycle control, and the ability to understand exactly how a frame reaches the screen.

---

# Supported Platforms

| Platform | Primary integration | Runtime model |
|---|---|---|
| Windows | WinForms or Avalonia | Engine-driven background loop |
| Linux | Avalonia | Engine-driven background loop |
| macOS | Avalonia | Engine-driven background loop |
| Browser / WebAssembly | Blazor | Timer-driven engine ticks |
| Gamepads | Optional SDL2 provider | Polled through the shared input subsystem |

The platform adapter projects handle render-surface creation, presentation, native input wiring, and application lifecycle integration. Game and engine code remain in the platform-neutral assemblies wherever possible.

---

# Key Technologies

| Technology | Role |
|---|---|
| .NET 8 / C# | Primary language and runtime |
| SkiaSharp | Cross-platform bitmap and GPU-backed 2D rendering |
| NAudio | Optional Windows audio backend in `Gondwana.Audio.NAudio` |
| HTML5 Audio / Web Audio / JavaScript interop | Browser backend in `Gondwana.Audio.Browser`, using the common core audio API |
| LibVLCSharp | Experimental video playback |
| SDL2 | Optional cross-platform gamepad input |
| WinForms | Windows desktop rendering and hosting |
| Avalonia | Cross-platform desktop rendering and hosting |
| Blazor / WebAssembly | Browser-hosted rendering and lifecycle integration |
| Nerdbank.GitVersioning | Deterministic versioning from `version.json` |

---

# Core Capabilities

## Scenes, Layers, Views, and Cameras

- Hierarchical scenes composed of ordered `SceneLayer` instances
- Independent parallax and refresh tracking per layer
- Multiple simultaneous `View` instances
- Camera movement, following, and interpolation
- Viewports suitable for split-screen, minimaps, HUDs, and alternate scene views
- Stable Z-ordering for predictable composition

## Tiles, Sprites, and Drawing

- Tilesheets, tile regions, animation frames, and cached image resources
- Sprites with movement, animation, collision, visual effects, and pixel overhang
- DirectDrawing primitives such as:
  - `DirectImage`
  - `DirectRectangle`
  - `TextBlock`
  - `DirectParticles`
- `DirectComposite` support for grouping related drawable elements
- `ImageInstanceLayer` for efficiently drawing many reusable bitmap instances
- Font and SVG resource management

## Movement and Animation

- Integrated movement
- Target following
- Scripted movement paths
- Easing-based tweening
- Smooth camera and sprite interpolation
- Frame-based tilesheet animation
- Visual effects such as jiggle, pulse, and resize loops

## Collisions and Kinematic Physics

- Bounding-volume collision detection
- Per-layer collision resolution
- Collision processing after sprite movement
- Tile collision metadata
- Per-tile collision-area adjustments
- Collision tracing and debugging support
- Kinematic push-out and movement correction

Gondwana currently emphasizes deterministic kinematic behavior rather than attempting to provide a full rigid-body physics simulation.

## Input

- Keyboard polling
- Mouse polling
- Touch input
- Optional SDL2 gamepad input
- Shared input abstractions across platform adapters
- Widget pointer and keyboard routing
- Pointer capture, focus, hit testing, clicking, and dragging for in-game widgets

## In-Game Widgets

`Gondwana.Widgets` provides reusable game-interface elements built on Gondwana's own rendering pipeline rather than native operating-system controls.

Widgets can be placed in a `View` or `SceneLayer` and can participate in the same composition and input systems as other game content. The library is intended for:

- Splash screens
- HUD elements
- Status indicators and health bars
- Labels and text panels
- Dialog and conversation interfaces
- Menus and selectable options
- Buttons and interactive overlays
- Sprite-adjacent interface elements

## Audio and Video

- Desktop audio playback and resource management
- Stereo panning
- Browser audio through HTML5 Audio and JavaScript interop
- MIDI playback and SoundFont synthesis through `Gondwana.Audio.Midi`
- Experimental video playback through `Gondwana.Video`

## Timing, Dispatch, and Extensibility

- High-resolution timers
- Scheduled pre-cycle and post-cycle callbacks
- Separate engine-thread and UI-thread dispatchers
- Engine lifecycle and frame events
- Plugin hooks around initialization, shutdown, cycles, and frame rendering
- CPS, FPS, and GPU frame-rate measurement

---

# Architecture at a Glance

```text
Game Code
    |
    v
Platform Game Host
    |
    +-- WinFormsGameHost
    +-- AvaloniaGameHost
    +-- BlazorGameHost
    |
    v
Engine
    |
    +-- Input and timers
    +-- Movement and animation
    +-- Collision resolution
    +-- Scenes and scene layers
    +-- Views, cameras, and viewports
    +-- DirectDrawing and widgets
    +-- Resource managers
    |
    v
RenderSurfaceHost
    |
    +-- BitmapBackbuffer
    +-- GpuBackbuffer
    |
    v
Platform Adapter
    |
    +-- WinForms control
    +-- Avalonia control
    +-- Blazor canvas / JavaScript interop
```

The core engine does not depend on the application framework. Platform-specific concerns remain at the edges, while the hosting projects provide convenient lifecycle orchestration.

---

# Solution Structure

The main solution is divided into focused runtime, adapter, tooling, demo, and testing projects.

## Core Runtime

### `Gondwana/`

The primary engine package.

Major areas include:

- `Engine`, `EngineManagers`, and `EngineDispatcher`
- Assets and resource management
- Audio abstractions
- Configuration and persistent engine state
- Drawing, sprites, tilesheets, text, SVG, and particles
- Input polling abstractions
- Movement and easing
- Collision detection and kinematic resolution
- Rendering, backbuffers, views, cameras, and viewports
- Scenes and hierarchical scene layers
- High-resolution timers
- Logging and extensibility

### `Gondwana.Hosting/`

Contains the platform-neutral `GameHostBase` lifecycle abstraction used by platform-specific hosts.

### `Gondwana.Widgets/`

Reusable, engine-rendered UI and gameplay widgets. This package depends on the core engine but not on WinForms, Avalonia, or Blazor.

---

## Platform Adapters and Hosts

### WinForms

- `Gondwana.WinForms/`
  - Windows render-surface adapter
  - Keyboard, mouse, and presentation wiring
  - Desktop audio integration

- `Gondwana.WinForms.Hosting/`
  - `WinFormsGameHost`
  - WinForms application lifecycle and engine startup glue

### Avalonia

- `Gondwana.Avalonia/`
  - Cross-platform desktop render-surface adapter
  - Avalonia input and presentation integration

- `Gondwana.Avalonia.Hosting/`
  - `AvaloniaGameHost`
  - Avalonia application lifecycle and engine startup glue

### Blazor

- `Gondwana.Blazor/`
  - WebAssembly render-surface adapter
  - Browser canvas presentation
  - JavaScript interop and browser input integration

- `Gondwana.Blazor.Hosting/`
  - `BlazorGameHost`
  - Timer-driven lifecycle integration for Blazor WebAssembly

---

## Optional Runtime Packages

- `Gondwana.Audio.NAudio/`
  - Windows implementation of the core audio contracts, including Vorbis and variable playback speed

- `Gondwana.Audio.Browser/`
  - Browser audio through HTML5 Audio and JavaScript interop

- `Gondwana.Audio.Midi/`
  - MIDI file support
  - SoundFont-based synthesis

- `Gondwana.Input.SDL2/`
  - Optional SDL2 gamepad provider

- `Gondwana.Video/`
  - Experimental video playback through LibVLCSharp

---

## Tooling

- `Tooling/Gondwana.Cli/`
  - Global `gondwana` command-line tool
  - Project creation, environment checks, asset operations, and template management

- `Tooling/Gondwana.Templates/`
  - `dotnet new` templates for WinForms, Avalonia, and Blazor projects

- `Tooling/Gondwana.Studio.Core/`
  - Framework-neutral Studio view models, services, and extension contracts

- `Tooling/Gondwana.Studio/`
  - Avalonia-based cross-platform Gondwana Studio

- `Tooling/Gondwana.Studio.WinForms/`
  - WinForms-based Gondwana Studio

- `Tooling/Gondwana.Assets.WinForms/`
  - WinForms asset tooling

Gondwana Studio is supplemental tooling. Gondwana projects remain ordinary .NET projects and do not require Studio to build or run.

---

## Demos and Tests

### Demos

- `Demos/Slider`
- `Demos/Spot`
- `Demos/SpotAvalonia`
- `Demos/Spot.Blazor`
- `Demos/Gondwana.CoordinateTest`
- `Demos/Gondwana.ParticleTest`

### Tests

- `Testing/Gondwana.Tests`
  - Unit and integration coverage for the core engine, rendering behavior, widgets, collisions, serialization, and related systems

---

# Runtime Cycle

Gondwana separates high-frequency engine updates from throttled foreground rendering.

## 1. Drain Engine-Thread Work

Queued work posted through `EngineDispatcher` is executed on the engine thread before the cycle proceeds.

## 2. Run Background Updates

Each background cycle performs the engine's simulation-oriented work:

1. Raise pre-cycle timer callbacks.
2. Poll keyboard, mouse, touch, and gamepad input.
3. Advance animated tiles.
4. Advance sprite movement.
5. Resolve collisions after movement.
6. Update cameras.
7. Raise background lifecycle events and plugin hooks.

The background cycle rate and rendered frame rate are tracked separately.

## 3. Render When Due

When the configured frame interval has elapsed, or when rendering is unbounded, the foreground pass:

1. Raises pre-render events and plugin hooks.
2. Updates DirectDrawing state.
3. Renders each registered surface to its backbuffer.
4. Presents each completed backbuffer through its platform adapter.
5. Updates gamepad state.
6. Raises post-render and post-cycle timer events.

GPU surfaces whose rendering must occur on a platform GL thread are coordinated through their platform-specific render path rather than forced through the standard CPU presentation loop.

## 4. Sample Performance

The engine periodically calculates:

- Gross engine cycles per second
- Presented frames per second
- GPU-rendered frame counts

## Desktop and WebAssembly Differences

Desktop hosts normally run the engine cycle on a background task.

Single-threaded WebAssembly environments instead use timer-driven startup. The Blazor host calls `Engine.Tick()` from the platform timer, allowing the same engine cycle to run without creating an unsupported background thread.

---

# Rendering Model

Gondwana uses two deliberately different rendering strategies.

## Bitmap Backbuffers

CPU-rendered bitmap surfaces use dirty-region rendering:

1. State changes enqueue world-space rectangles into the owning `SceneLayer`'s `RefreshQueue`.
2. Each active `View` transforms relevant world regions into its viewport.
3. Only the affected regions are redrawn.
4. The completed bitmap backbuffer is presented by the platform adapter.

This avoids repainting the entire surface when only a small portion of the scene changed.

## GPU Backbuffers

GPU-backed Skia surfaces use a full-viewport rendering path better suited to GPU synchronization and platform GL constraints.

The GPU path does not imitate the bitmap dirty-rectangle pipeline merely for architectural symmetry. Each backbuffer type is allowed to use the strategy appropriate to its actual rendering hardware.

## World-Space First

Game state, movement, collisions, and refresh tracking are expressed in world coordinates. Camera and viewport transforms are applied during view rendering.

This keeps gameplay logic independent of a particular window size, camera position, viewport, or platform adapter.

---

# Coordinate Systems

Gondwana supports several grid and projection models:

| Coordinate system | Description |
|---|---|
| Orthogonal | Axis-aligned square grid |
| Isometric Rhombic | Diamond-lattice isometric projection |
| Isometric Axial | Isometric projection over a diagonally oriented square lattice |
| Hex Axial Flat Top | Axial coordinates with flat-topped hexagons |
| Hex Axial Pointed Top | Axial coordinates with pointy-topped hexagons |
| Oblique | Sheared square lattice with right-receding depth |

Coordinate conversion remains centralized so scenes, layers, sprites, tiles, movement, collisions, and views can share the same world-space model.

---

# Packages and Tooling

Install only the packages required by the target application.

## Runtime Packages

| Package | Purpose |
|---|---|
| `Gondwana` | Core engine |
| `Gondwana.Hosting` | Platform-neutral host lifecycle |
| `Gondwana.Widgets` | In-game widgets and UI components |
| `Gondwana.WinForms` | WinForms render and input adapter |
| `Gondwana.WinForms.Hosting` | Ready-to-use WinForms game host |
| `Gondwana.Avalonia` | Avalonia render and input adapter |
| `Gondwana.Avalonia.Hosting` | Ready-to-use Avalonia game host |
| `Gondwana.Blazor` | Blazor/WebAssembly adapter |
| `Gondwana.Blazor.Hosting` | Blazor game host and timer-driven lifecycle |
| `Gondwana.Audio.NAudio` | Windows NAudio backend for the common audio API |
| `Gondwana.Audio.Browser` | Browser backend for the common audio API |
| `Gondwana.Audio.Midi` | Windows MIDI and SoundFont support layered on NAudio |
| `Gondwana.Input.SDL2` | SDL2 gamepad provider |
| `Gondwana.Video` | Experimental video playback |

## Developer Tools

### Install Project Templates

```powershell
dotnet new install Gondwana.Templates
```

Available templates include:

- `gondwana-winforms`
- `gondwana-avalonia`
- `gondwana-blazor`

### Install the CLI

```powershell
dotnet tool install --global Gondwana.Cli
```

The CLI command is:

```powershell
gondwana
```

Use the CLI to create projects, inspect the development environment, work with asset packages, and manage templates.

---

# Key Design Principles

## Code First, Tooling Optional

The source project remains authoritative. Tools assist development but do not own the game.

## Explicit Engine Lifecycle

Initialization, updates, rendering, dispatch, and shutdown have visible, inspectable control flow.

## World-Space Logic

Gameplay code operates in world coordinates; view transforms remain a rendering concern.

## Different Backbuffers, Different Strategies

Bitmap and GPU surfaces use optimization paths suited to their actual characteristics.

## Layered, View-Centric Rendering

Scenes are composed from layers, while views determine how those layers are observed and presented.

## Platform Adapters at the Edges

The core engine remains independent of WinForms, Avalonia, and Blazor.

## Deterministic Ordering

Views, layers, sprites, tiles, DirectDrawing objects, and widgets use stable ordering rules where draw order matters.

## Composition Without Mandatory ECS

Gondwana favors focused engine objects and composable subsystems. Game-specific entity and component models can be added where they provide real value rather than being imposed universally.

## Practical Extensibility

Plugins, dispatchers, overridable lifecycle hooks, adapters, resource managers, and separate packages provide extension points without turning the core into a dependency grab bag.

---

# Versioning

Gondwana uses **Nerdbank.GitVersioning**.

- The canonical version is defined in `version.json`.
- Version metadata is applied automatically during the build.
- Shared build and package metadata is centralized through `Directory.Build.props` and `Directory.Build.targets`.
- Project files should not hard-code `<Version>` or `<FileVersion>` values.
- CI builds enable deterministic build behavior and Source Link-compatible package metadata.

---

# Where to Go Next

- New to Gondwana? [[Make Your First Game in 30 Minutes]]
- Learn the core mental model [[Engine Architecture Overview]]
- Browse the API https://isthimius.github.io/Gondwana/
- Install the core package https://www.nuget.org/packages/Gondwana
- Review recent changes https://github.com/Isthimius/Gondwana/blob/master/CHANGELOG.md
- Explore the source https://github.com/Isthimius/Gondwana
