# Gondwana
<img src="https://github.com/user-attachments/assets/64372678-7d38-47f8-b01a-511c3ef407cc"
     alt="Gondwana" align="left" width="40%" />

**Gondwana** is a cross-platform 2D game and rendering engine written in C#/.NET 8, built around SkiaSharp for graphics, and NAudio for sound, and includes hooks for in-app video playback. It modernizes legacy Win32/GDI patterns into a modular, high-performance framework that runs on desktop, mobile, and web.

The engine supports both bitmap- and GPU-based rendering back-ends and maintains a clean, extensible draw pipeline designed for compositing, layering, and post-processing effects. Input handling is unified across keyboard, mouse, and gamepad devices through a common event-polling interface.

Under the hood, Gondwana employs a double-buffered rendering system, fine-grained timing controls, and thread-safe managers for resources such as tilesheets, sprites, scenes, and particle systems. Its architecture is built around modern .NET, allowing developers to write game logic that’s both performant and maintainable.

The result is a framework that feels low-level enough for engine tinkerers but high-level enough for rapid game prototyping — bridging the gap between classic 2D engines and modern cross-platform development.

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

```
Gondwana/
├── Gondwana/               # Core engine: timing, math, resource management
├── Gondwana.Rendering/     # SkiaSharp rendering & backbuffer system
├── Gondwana.Audio/         # Audio playback (WAV, MP3, OGG via NAudio)
├── Gondwana.WinForms/      # Windows desktop adapter (SKControl integration)
├── Gondwana.Web/           # Browser/WebAssembly adapter
└── Examples/               # Sample projects (Hello World, Particles, Sprites)
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
