# Gondwana
<img src="https://github.com/user-attachments/assets/64372678-7d38-47f8-b01a-511c3ef407cc"
     alt="Gondwana" align="left" width="40%" />

**Gondwana** is a cross-platform 2.5D game and rendering engine written in C#/.NET 8, supporting scene parallax, z-ordering, pixel overhang, collision detection, and a particle system. The framework is built with SkiaSharp for graphics, NAudio for sound, and includes hooks for in-app video playback. It modernizes legacy Win32/GDI patterns into a modular, high-performance framework that runs on desktop, mobile, and web.

The engine supports both bitmap- and GPU-based rendering back-ends and maintains a clean, extensible draw pipeline designed for compositing, layering, and post-processing effects. Input handling is unified across keyboard, mouse, and gamepad devices through a common event-polling interface.

Under the hood, Gondwana employs a double-buffered rendering system, fine-grained timing controls, and thread-safe managers for resource caching of tilesheets, audio, and video. Its architecture is built using modern .NET, allowing developers to quickly create games with code that’s both performant and maintainable.

<br clear="left" />

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

At runtime, Gondwana is driven by a central Engine loop that advances time, polls platform-specific input, updates game state, and renders only what has changed. Each cycle begins by processing timers and input events, which may move sprites, advance animations, or otherwise modify scene state. These changes enqueue world-space dirty regions into each SceneLayer’s RefreshQueue. During rendering, the ViewRenderer iterates views in Z-order, applies camera and viewport transforms, and asks each visible layer to redraw only the affected regions into a backbuffer. Sprites and tiles are drawn from cached tilesheets, animations advance frame-by-frame, and the composed result is finally presented by the platform host (WinForms, Web, etc.). This dirty-region, view-centric design keeps rendering efficient while allowing multiple views and cameras to share the same core engine logic.

**Key Design Principles**
- Dirty-region rendering (RefreshQueue): The engine tracks what changed and redraws only those world-space regions, instead of repainting the whole screen every frame.
- World-space first: The engine reasons in world pixels; views/cameras/viewport transforms convert world → screen at render time. This keeps logic consistent and avoids “screen math” leaking into gameplay code.
- Layered scenes: A Scene is composed of SceneLayers (often with parallax). Each layer maintains its own refresh tracking and draw path.
- View-centric rendering: Rendering flows through View / ViewRenderer so multiple cameras/viewports (or future split views) are natural, not bolted on.
- Adapters at the edges: Platform projects (WinForms/Web) host the render surface and input wiring, while the core engine stays platform-agnostic.
- Deterministic ordering: Where ordering matters (views, layers, drawables), the engine uses stable sort rules so rendering remains predictable and debuggable.

```
Gondwana
├── Gondwana
│   ├── Gondwana.Engine              # Core runtime loop and timing orchestration
│   │   ├── Engine                   # Central coordinator: cycle, timing, background tasks
│   │   ├── Game                     # Game-facing entry point and lifecycle wrapper
│   │   └── EngineTimer              # High-resolution timing and tick management
│   │
│   ├── Gondwana.Scene               # World organization and visibility
│   │   ├── Scene                    # Root container for layers and world state
│   │   ├── SceneLayer               # Logical/renderable layer with parallax and refresh tracking
│   │   └── SceneLayerCollection     # Ordered management of visible layers
│   │
│   ├── Gondwana.Refresh             # Dirty-region tracking and redraw coordination
│   │   └── RefreshQueue             # World-pixel dirty rectangle accumulator
│   │
│   ├── Gondwana.Sprites             # Dynamic drawable entities
│   │   ├── Sprite                   # Movable, animatable visual entity
│   │   ├── SpriteManager            # Global sprite registration and spatial queries
│   │   └── SpriteDrawInfo           # Precomputed draw metadata for rendering passes
│   │
│   ├── Gondwana.Tiles               # Tile-based rendering infrastructure
│   │   ├── Tile                     # Individual tile instance in world space
│   │   ├── Tilesheet                # Source bitmap and tile slicing logic
│   │   └── TileCache                # Cached tile bitmaps for fast redraw
│   │
│   ├── Gondwana.Animation           # Time-based visual state changes
│   │   ├── Animation                # High-level animation controller
│   │   ├── AnimationCycle           # Ordered sequence of animation frames
│   │   └── AnimationFrame           # Single frame definition and duration
│   │
│   ├── Gondwana.Movement            # Position and velocity updates
│   │   ├── MovementController       # Applies movement logic to sprites
│   │   └── Velocity                 # Directional and scalar movement data
│   │
│   ├── Gondwana.Collision           # Spatial interaction and resolution
│   │   ├── Collider                 # Collision bounds and masks
│   │   └── CollisionResolver        # Collision detection and response logic
│   │
│   ├── Gondwana.Input               # Engine-facing input abstraction
│   │   ├── KeyboardEventPoller      # Keyboard state polling and event dispatch
│   │   ├── MouseEventPoller         # Mouse state polling and event dispatch
│   │   └── GamepadEventPoller       # Gamepad polling with throttling
│   │
│   ├── Gondwana.Math                # Shared math and geometry helpers
│   │   ├── Vector                   # Basic vector math
│   │   ├── RectangleExtensions      # Geometry helpers and conversions
│   │   └── CoordinateHelpers        # World/screen coordinate utilities
│   │
│   └── Gondwana.Util                # Cross-cutting utilities
│       ├── Logger                   # Centralized logging and tracing
│       └── DisposableBase           # Lifetime and disposal helpers
│
├── Gondwana.Rendering
│   ├── Gondwana.Rendering.View      # View and render orchestration
│   │   ├── View                     # Camera + viewport pairing
│   │   ├── ViewRenderer             # Ordered rendering across views
│   │   └── ViewCollection           # Deterministic view management
│   │
│   ├── Gondwana.Rendering.Camera    # World-to-view transformations
│   │   ├── Camera                   # Position, zoom, and parallax anchor
│   │   └── CameraPanMode            # Camera movement semantics
│   │
│   ├── Gondwana.Rendering.Viewport  # Screen-space mapping
│   │   ├── Viewport                 # Screen rectangle and zoom configuration
│   │   └── ViewportTransform        # Coordinate conversion logic
│   │
│   ├── Gondwana.Rendering.Backbuffer    # Offscreen render targets
│   │   ├── IBackbuffer              # Backbuffer abstraction
│   │   ├── BitmapBackbuffer         # CPU-backed Skia bitmap buffer
│   │   └── GpuBackbuffer            # GPU-backed render buffer (when enabled)
│   │
│   ├── Gondwana.Rendering.Drawing   # Immediate-mode drawing system
│   │   ├── DirectDrawingBase        # Base drawable primitive
│   │   ├── DirectComposite          # Composite drawable with child elements
│   │   └── DirectDrawingManager     # Registration and draw ordering
│   │
│   └── Gondwana.Rendering.Skia      # SkiaSharp integration details
│       ├── SkiaHelper               # Bitmap and paint helpers
│       └── SkiaPaintCache           # Reusable paint objects for performance
│
├── Gondwana.Audio
│   ├── Gondwana.Audio.Core          # Audio asset management
│   │   ├── MediaFile                # Audio asset loading and lifetime
│   │   └── AudioManager             # Playback coordination
│   │
│   └── Gondwana.Audio.Playback      # Runtime playback instances
│       ├── AudioInstance            # Individual sound playback handle
│       └── PlaybackCompletedEventArgs    # Notification payload for completed sounds
│
├── Gondwana.WinForms
    ├── Gondwana.WinForms.Host       # Desktop hosting infrastructure
    │   ├── RenderSurfaceHost        # Bridge between engine and WinForms surface
    │   └── GameWindow               # Application window and lifecycle
    │
    ├── Gondwana.WinForms.Rendering  # WinForms rendering surface
    │   └── SkiaRenderSurface        # SKControl-backed render target
    │
    └── Gondwana.WinForms.Input      # Platform input adapters
        ├── WinFormsKeyboardAdapter  # Keyboard adapter for engine input
        └── WinFormsMouseAdapter     # Mouse adapter for engine input
```

---

## 📦 Prerequisites

The Gondwana Core library depends on the following NuGet packages:

- **Microsoft.Extensions.Configuration** (9.0.8)  
- **Microsoft.Extensions.Configuration.Binder** (9.0.8)  
- **Microsoft.Extensions.Configuration.Json** (9.0.8)  
- **Microsoft.Extensions.Logging.Console** (9.0.8)  
- **Microsoft.Extensions.Logging.Debug** (9.0.8)  
- **NAudio** (2.2.1) — audio playback and mixing  
- **Newtonsoft.Json** (13.0.3) — JSON serialization  
- **SharpZipLib** (1.4.2) — archive and compression support  
- **SkiaSharp** (3.119.0) — 2D rendering backend  
- **SkiaSharp.HarfBuzz** (3.119.0) — advanced text shaping/rendering


## 🏃‍♂️ Build & Run
```bash
git clone https://github.com/yourusername/gondwana.git
cd gondwana
dotnet build
```

Run one of the examples:
```bash
cd Examples/HelloWorld
dotnet run
```

---

## 🎮 Example: Particle System

```csharp
var particles = new DirectParticles(renderHost, 
    new Rectangle(0, 0, viewportW, viewportH));

// Sparks
var sparks = new ParticleEmitter
{
    Position = new PointF(400, 550),
    EmitRate = 400,
    LifeRange = (0.5f, 1.0f),
    VelocityRangeX = (-150f, 150f),
    VelocityRangeY = (-300f, -200f),
    SizeRange = (2f, 4f),
    Color = SKColors.OrangeRed
};

// Smoke
var smoke = new ParticleEmitter
{
    Position = new PointF(400, 540),
    EmitRate = 120,
    LifeRange = (2.5f, 4.0f),
    VelocityRangeX = (-40f, 40f),
    VelocityRangeY = (-120f, -60f),
    SizeRange = (8f, 16f),
    Color = new SKColor(80, 80, 80, 200)
};

particles.Emitters.Add(sparks);
particles.Emitters.Add(smoke);
directDrawingManager.AddOrReplace(particles);
```

---

## 🛠 Roadmap

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
