# Where to Start Reading — Gondwana Engine Onboarding

A recommended reading order for getting up to speed on the Gondwana engine codebase quickly.

---

## ⚡ Optional: Automated Dev Setup (Windows / PowerShell)

If you're on Windows and want to get everything installed in one shot, run the setup script from anywhere inside the cloned repository:

```powershell
# Full setup — installs .NET 8 SDK (if missing), restores packages, builds the
# solution, installs the Gondwana CLI + templates, WASM workload, and checks
# for optional native libraries (SDL2, LibVLC).
.\Solution Items\scripts\Setup-Gondwana-Dev.ps1

# Core-only — skips the WASM workload, SDL2, and LibVLC checks
.\Solution Items\scripts\Setup-Gondwana-Dev.ps1 -SkipOptional
```

The script is idempotent — safe to re-run at any time. It ends with `gondwana doctor` to confirm your environment is healthy.

> **This step is entirely optional.** Everything the script does can also be done manually with `dotnet restore`, `dotnet build`, and `dotnet tool install`. See [`Solution Items/scripts/README.md`](Solution%20Items/scripts/README.md) for a full description of all available scripts.

---

## 🗺️ Reading Order

### 1. Get the Big Picture First

**`README.md`** — Read the whole thing (~5 min). It explains the engine's philosophy (dirty-region rendering, world-space first, code-first design), the runtime flow, and lists all namespaces with their responsibilities. This is the best single document in the repo.

---

### 2. Understand the Host/Lifecycle Contract

**`Gondwana.Hosting/GameHostBase.cs`** — The abstract base class for all games. Only ~260 lines, well-documented, and shows the exact lifecycle of a game:

```
Initialize()
  → ConfigureLogging → ConfigurePlatform → ConfigureInput
  → InitializeGameContent
       → LoadContent → CreateSceneGraph → BindScene → InitializeSceneObjects
  → InitializeEngine
  → StartEngine
```

This tells you *every extension point* a game author uses before looking at any game-specific code.

---

### 3. Read a Real Demo End-to-End

**`Demos/Spot/SpotGameHost.cs`** + **`Demos/Spot/GameWindow.cs`** — This is the most complete working game in the repo. Read `SpotGameHost` to see how a real game wires up assets, scene layers, tilesheets, sprites, audio, input, and particle systems. It subclasses `WinFormsGameHost`, so you also see a concrete platform adapter in practice.

---

### 4. Understand the Core Engine Loop

**`Gondwana/Engine.cs`** — The singleton engine. Read the event declarations (~lines 76–210) to understand the hooks (`BeforeBackgroundTasksExecute`, `BeforeFrameRender`, `AfterFrameRender`, etc.), then skim `DoBackgroundTasks` and `DoForegroundTasks` to see what happens every cycle.

> **Concurrency note:** `Engine.Start` runs the main loop (`Cycle` / `DoForegroundTasks`) on a background `Task`, not the UI thread.

---

### 5. Understand the Rendering Model (3 files, in order)

1. **`Gondwana/Rendering/RefreshQueue.cs`** (~95 lines) — The core dirty-region mechanism. Everything that changes enqueues a world-pixel rectangle here. Short, well-commented, and critical to understand.

2. **`Gondwana/Rendering/Views/View.cs`** — `View` = Camera + Viewport. Read the coordinate-conversion methods (`ScreenPxToWorldPx`, `WorldPxToScreenPx`, `WorldRectToScreenRect`, etc.) — this is the math that connects world-space to screen-space. Understanding this file means understanding the rendering model.

3. **`Gondwana/Rendering/Views/Camera.cs`** + **`Viewport.cs`** — Skim after `View.cs` to understand camera position/clamping and viewport zoom/offset.

---

### 6. Understand the Scene Graph

**`Gondwana/Scenes/SceneLayer.cs`** — The primary data structure for game content. A `SceneLayer` is a grid of tiles with its own coordinate system, parallax factor, and `RefreshQueue`. Understanding this explains how sprites, tiles, and parallax backgrounds coexist.

---

### 7. Drawing Primitives (pick what you need)

| File/Namespace | What it covers |
|---|---|
| `Gondwana/Drawing/Direct/DirectDrawingManager.cs` | Thread-safe manager for overlay drawables (HUD, particles, text) |
| `Gondwana/Drawing/Sprites/` | How sprites attach to scene layers and are positioned in world-space |
| `Gondwana/Drawing/Tilesheets/` | How image atlases are defined and how sprites reference frame regions |

---

### 8. Platform Adapter (WinForms)

**`Gondwana.WinForms/Rendering/`** — Only read if you're working on the host layer. Shows how a WinForms control wraps the SkiaSharp backbuffer and wires up paint/resize events.

---

## Quick-Reference Mental Model

```
GameHostBase
  └─ Engine (singleton, loop)
       ├─ Scene
       │    └─ SceneLayer (grid + parallax + RefreshQueue)
       │         └─ SceneLayerTile → Sprite → Tilesheet frame
       ├─ ViewManager
       │    └─ View (Camera + Viewport)  ← world↔screen math lives here
       ├─ DirectDrawingManager           ← HUD/overlays (not in scene grid)
       ├─ Input (Keyboard / Mouse / Gamepad)
       └─ Timers / Collisions / Animations
```

**The key insight:** state changes call `RefreshQueue.AddWorldRect()` → the engine's render pass picks it up → `View` transforms the world rect to screen-space → the platform host repaints only that screen region.
