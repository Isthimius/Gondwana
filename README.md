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
├── Gondwana.Core/          # Core engine: timing, math, resource management
├── Gondwana.Rendering/     # SkiaSharp rendering & backbuffer system
├── Gondwana.Audio/         # Audio playback (WAV, MP3, OGG via NAudio)
├── Gondwana.WinForms/      # Windows desktop adapter (SKControl integration)
├── Gondwana.Web/           # Browser/WebAssembly adapter
└── Examples/               # Sample projects (Hello World, Particles, Sprites)
```

---

## 🚀 Getting Started

### Prerequisites
- .NET 8 SDK  
- SkiaSharp  
- (Optional) LibVLCSharp, NAudio  

### Build & Run
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
