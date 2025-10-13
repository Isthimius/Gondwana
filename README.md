<p align="center">
  <img src="https://github.com/user-attachments/assets/9381bf49-c5a7-4f81-8173-6debd01e073a" alt="Gondwana" width="80%">
</p>

# Gondwana Game Engine

**Gondwana** is a cross-platform 2D game and rendering engine written in **C#/.NET 8**, built around **SkiaSharp** for graphics. It modernizes legacy Win32/GDI patterns into a modular, high-performance framework that runs on desktop, mobile, and web.  

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

## 🚀 Getting Started

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
