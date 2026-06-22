<!-- Combined Gondwana Wiki — generated 2026-06-22 -->

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


---

# Make Your First Game in 15 Minutes with Gondwana

This guide walks you through building a small but complete desktop game using the Gondwana engine. By the end you will have a running WinForms window, a sprite on a grid, and keyboard-driven movement — all the pieces you need to start building something real.

The completed example mirrors the structure of the **Spot** demo included in this repository (`Demos/Spot/`). Spot is a good next read once you finish this guide.

---

## Contents

- [What You'll Build](#what-youll-build)
- [Prerequisites](#prerequisites)
- [Method A — Manual setup](#method-a--manual-setup)
- [Method B — Using the Gondwana CLI](#method-b--using-the-gondwana-cli)
- [What the Spot Demo adds](#what-the-spot-demo-adds)
- [Key mental model](#key-mental-model)
- [Further reading](#further-reading)

---

## What You'll Build

A tiny game called **Wanderer**: a coloured bubble sits on an 8×8 grid. The player moves it one cell at a time with the arrow keys. The bubble animates smoothly between cells.

Concepts covered:

| Concept | Where it shows up |
|---|---|
| Project & NuGet setup | Steps 1–2 |
| `GameHostBase` lifecycle | Steps 3–4 |
| Tilesheets & sprites | Step 5 |
| Scene & scene layer | Step 6 |
| Keyboard input | Step 7 |
| Scripted (animated) movement | Step 7 |
| Clean shutdown | Step 8 |

---

## Prerequisites

- .NET 8 SDK
- Visual Studio 2022 **or** the .NET CLI
- A square PNG image for your sprite (64×64 px is a good size — you can borrow `bubble-blue.png` from `Demos/Spot/assets/`)

---

## Method A — Manual setup

<details>
<summary>Click to expand</summary>

### Step 1 — Create the project (2 min)

```bash
dotnet new winforms -n Wanderer -f net8.0-windows
cd Wanderer
```

Add the Gondwana packages:

```bash
dotnet add package Gondwana
dotnet add package Gondwana.Hosting
dotnet add package Gondwana.WinForms
dotnet add package Gondwana.WinForms.Hosting
```

Copy your sprite PNG into an `assets\` subfolder and mark it as content in `Wanderer.csproj`:

```xml
<ItemGroup>
  <Content Include="assets\bubble-blue.png">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

---

### Step 2 — Wire up the entry point (1 min)

Replace the generated `Program.cs` with the standard WinForms startup pattern:

```csharp
using System;
using System.Windows.Forms;

namespace Wanderer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new GameWindow());
    }
}
```

---

### Step 3 — Create the game window (2 min)

`GameWindow` is a plain `Form`. Its only job is to own the render surface control and hand it to the game host at the right moment in the form's lifecycle.

```csharp
using System;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace Wanderer;

internal sealed class GameWindow : Form
{
    // WinFormBitmapRenderSurfaceControl is the SkiaSharp-backed render control
    // provided by Gondwana.WinForms.  Add it in the designer or create it here.
    private readonly Gondwana.WinForms.Rendering.WinFormBitmapRenderSurfaceControl _renderSurface = new();
    private WandererGameHost? _host;

    internal GameWindow()
    {
        this.Text          = "Wanderer";
        this.ClientSize    = new System.Drawing.Size(640, 640);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimizeBox   = false;
        this.MaximizeBox   = false;

        _renderSurface.Dock = DockStyle.Fill;
        Controls.Add(_renderSurface);
    }

    // Create the host once the form and all controls exist.
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _host = new WandererGameHost(_renderSurface);
    }

    // Initialize AFTER the form is visible — this provides a valid
    // SynchronizationContext which the engine requires.
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _host!.Initialize(logLevel: LogLevel.Warning);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _host?.Dispose();
        _host = null;
        base.OnFormClosed(e);
    }
}
```

> **Why OnShown?**  
> `Initialize` calls `Engine.Start`, which needs `SynchronizationContext.Current`. That context is only guaranteed to be set once the message loop is running, which is after `OnShown`.

---

### Step 4 — Understand the host lifecycle

`WandererGameHost` will subclass `WinFormsGameHost`. The full call sequence when `Initialize()` is called is:

```
Initialize()
  → ConfigureLogging
  → ConfigurePlatform          ← wired by WinFormsGameHost for you
  → ConfigureInput
        → ConfigureKeyboard     ← you override this
  → LoadContent
        → LoadAssets
        → LoadTilesheets        ← you override this
  → CreateSceneGraph           ← you override this
  → InitializeSceneObjects
        → CreateDirectDrawings  ← optional overlay HUD
  → InitializeEngine
  → StartEngine
        → OnStartEngine         ← optional post-start hook
```

You only need to override the methods you care about. Everything else has a safe no-op default.

---

### Step 5 — Load the tilesheet and create a sprite (3 min)

```csharp
using System.Drawing;
using System.Windows.Forms;
using Gondwana;
using Gondwana.Drawing;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Movement.Easing;
using Gondwana.Scenes;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Input.Keyboard;
using Gondwana.WinForms.Rendering;

namespace Wanderer;

internal sealed class WandererGameHost : WinFormsGameHost
{
    // --- grid dimensions ---
    private const int Columns = 8;
    private const int Rows    = 8;
    private const int CellPx  = 64;   // pixel size of one grid cell

    // --- runtime state ---
    private Tilesheet _bubbleTilesheet = null!;
    private Sprite    _sprite          = null!;
    private int       _gridX           = 0;   // current sprite column
    private int       _gridY           = 0;   // current sprite row
    private bool      _isMoving        = false;

    internal WandererGameHost(WinFormBitmapRenderSurfaceControl renderSurface)
        : base(renderSurface) { }

    // ── Step 5a: load the tilesheet ──────────────────────────────────────
    protected override void LoadTilesheets()
    {
        // A Tilesheet is an image atlas.  TileSize tells Gondwana how large
        // one frame is.  A single-frame image is the simplest possible case.
        _bubbleTilesheet = new Tilesheet("bubble", @"assets\bubble-blue.png");
        _bubbleTilesheet.TileSize = new Size(92, 96); // adjust to match your PNG
    }
```

---

### Step 6 — Build the scene and place the sprite (3 min)

```csharp
    // ── Step 6a: create the scene ─────────────────────────────────────────
    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();

        // AddLayer(columns, rows, tileWidth, tileHeight, zOrder, parallax, coordinateSystem)
        scene.AddLayer(
            columnCount:      Columns,
            rowCount:         Rows,
            width:            CellPx,
            height:           CellPx,
            zOrder:           10,
            parallax:         1f,
            coordinateSystem: Gondwana.Drawing.Coordinates.CoordinateSystemTypes.Orthogonal);

        scene[0].ShowGridLines = true;   // helpful while building
        return scene;
    }

    // ── Step 6b: place the sprite ─────────────────────────────────────────
    protected override void CreateSceneGraph()
    {
        base.CreateSceneGraph(); // sets this.Scene

        var layer = Scene![0];

        // Frame = (tilesheet, column, row) into the atlas.
        // Column 0, Row 0 is the first (and here only) frame.
        var frame = new Frame(_bubbleTilesheet, 0, 0);

        _sprite = SpriteManager.Instance.CreateSprite(layer, frame);
        _sprite.SetPosition(new(_gridX, _gridY));
        _sprite.RenderSize = new Size(56, 56);   // slightly smaller than the cell
        _sprite.VertAlign  = VerticalAlignment.Middle;
        _sprite.Visible    = true;
    }
```

---

### Step 7 — Move the sprite with arrow keys (3 min)

Gondwana's keyboard adapter fires events on the engine thread. Override `OnKeyboardAdapterInitialized` to subscribe **after** the adapter is ready, and explicitly tell it which keys to watch.

```csharp
    // ── Step 7a: subscribe to keyboard events ─────────────────────────────
    protected override void OnKeyboardAdapterInitialized()
    {
        if (Engine.Input.KeyboardEventPoller is null)
            return;

        Engine.Input.KeyboardEventPoller.KeyDown += OnKeyDown;

        // Register every key you want to receive events for.
        Engine.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.Left);
        Engine.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.Right);
        Engine.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.Up);
        Engine.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.Down);
    }

    protected override void UnhookEvents()
    {
        if (Engine.Input.KeyboardEventPoller is not null)
            Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;
    }

    // ── Step 7b: handle key presses ───────────────────────────────────────
    private void OnKeyDown(KeyDownEventArgs args)
    {
        // Only act on the initial press, not auto-repeat.
        if (args.KeyAction != KeyAction.Pressed)
            return;

        // Block input while a move animation is in progress.
        if (_isMoving)
            return;

        var key = WinFormsKeyboardAdapter.GetKeyFromString(args.KeyConfig.Key);

        int newX = _gridX;
        int newY = _gridY;

        switch (key)
        {
            case Keys.Left:  newX--; break;
            case Keys.Right: newX++; break;
            case Keys.Up:    newY--; break;
            case Keys.Down:  newY++; break;
            default: return;
        }

        // Clamp to the grid.
        if (newX < 0 || newX >= Columns || newY < 0 || newY >= Rows)
            return;

        _gridX    = newX;
        _gridY    = newY;
        _isMoving = true;

        // MoveTo(destination, durationSeconds, easing, delaySeconds)
        _sprite.Movement.ScriptedMovementStopped += OnMovementStopped;
        _sprite.Movement.MoveTo(
            new(_gridX, _gridY),
            0.15f,
            EasingKind.SmootherStep,
            0f);
    }

    private void OnMovementStopped(Gondwana.Movement.Scripted.ScriptedMovement _)
    {
        _sprite.Movement.ScriptedMovementStopped -= OnMovementStopped;
        _isMoving = false;
    }
}  // end of WandererGameHost
```

---

### Step 8 — Run it

```bash
dotnet run
```

You should see an 8×8 grid with a bubble in the top-left corner. Arrow keys glide it across the grid.

> **Tip:** press `F5` in Visual Studio to get a debugger-attached run. Gondwana logs at `LogLevel.Warning` by default; bump it to `LogLevel.Debug` in `Initialize()` to see per-frame event output.

</details>

---

## Method B — Using the Gondwana CLI

<details>
<summary>Click to expand</summary>

The Gondwana CLI scaffolds the boilerplate for you — no manual NuGet installs or hand-written `Program.cs` / `GameWindow.cs`. You write only the game-specific logic.

**Prerequisite:** install the CLI and templates once:

```bash
dotnet tool install --global Gondwana.Cli
gondwana templates install
```

---

### Step 1 — Scaffold the project (1 min)

```bash
gondwana new winforms Wanderer
cd Wanderer
```

The CLI creates:

| File | What it contains |
|---|---|
| `Wanderer.csproj` | All four Gondwana NuGet packages pre-referenced, plus a commented `<Content>` example for assets |
| `Program.cs` | Standard `[STAThread]` WinForms entry point |
| `GameWindow.cs` | `Form` with render surface wired to the host lifecycle |
| `GameHost.cs` | `WandererGameHost : WinFormsGameHost` with stub override methods ready to fill in |
| `assets/README.txt` | Instructions for adding sprite files |

---

### Step 2 — Add your sprite (1 min)

Copy your PNG into the `assets\` subfolder, then uncomment (or add) the `<Content>` block in `Wanderer.csproj`:

```xml
<ItemGroup>
  <Content Include="assets\bubble-blue.png">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

> The `.csproj` already has a commented-out example for exactly this.

---

### Step 3 — Understand the host lifecycle

`GameWindow.cs` is already wired correctly (`OnLoad` → `OnShown` → `OnFormClosed`). `WandererGameHost.Initialize()` runs this sequence:

```
Initialize()
  → ConfigureLogging
  → ConfigurePlatform
  → ConfigureInput
  → LoadContent
  → CreateSceneGraph
  → InitializeSceneObjects
  → InitializeEngine
  → StartEngine
```

No edits are required here unless you want custom startup behavior.

---

### Steps 4–7 — Fill in `GameHost.cs`

Open `GameHost.cs`. The scaffolded `WandererGameHost` already has the right base class and empty overrides — add the field declarations and fill in each method below.

Add these fields at the top of the class:

```csharp
private const int Columns = 8;
private const int Rows    = 8;
private const int CellPx  = 64;

private Tilesheet _bubbleTilesheet = null!;
private Sprite    _sprite          = null!;
private int       _gridX           = 0;
private int       _gridY           = 0;
private bool      _isMoving        = false;
```

Then fill in the overrides:

**`LoadTilesheets`**:
```csharp
protected override void LoadTilesheets()
{
    _bubbleTilesheet = new Tilesheet("bubble", @"assets\bubble-blue.png");
    _bubbleTilesheet.TileSize = new System.Drawing.Size(92, 96);
}
```

**`CreateInitialScene`**:
```csharp
protected override Scene CreateInitialScene()
{
    var scene = new Scene();
    scene.AddLayer(
        columnCount: Columns, rowCount: Rows,
        width: CellPx, height: CellPx,
        zOrder: 10, parallax: 1f,
        coordinateSystem: Gondwana.Drawing.Coordinates.CoordinateSystemTypes.Orthogonal);
    scene[0].ShowGridLines = true;
    return scene;
}
```

**`CreateSceneGraph`**:
```csharp
protected override void CreateSceneGraph()
{
    base.CreateSceneGraph();
    var frame = new Frame(_bubbleTilesheet, 0, 0);
    _sprite = SpriteManager.Instance.CreateSprite(Scene![0], frame);
    _sprite.SetPosition(new(_gridX, _gridY));
    _sprite.RenderSize = new System.Drawing.Size(56, 56);
    _sprite.VertAlign  = VerticalAlignment.Middle;
    _sprite.Visible    = true;
}
```

**`OnKeyboardAdapterInitialized`, `UnhookEvents`, and movement handlers**:

```csharp
protected override void OnKeyboardAdapterInitialized()
{
    if (Engine.Input.KeyboardEventPoller is null)
        return;

    Engine.Input.KeyboardEventPoller.KeyDown += OnKeyDown;
    Engine.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.Left);
    Engine.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.Right);
    Engine.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.Up);
    Engine.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.Down);
}

protected override void UnhookEvents()
{
    if (Engine.Input.KeyboardEventPoller is not null)
        Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;
}

private void OnKeyDown(KeyDownEventArgs args)
{
    if (args.KeyAction != KeyAction.Pressed || _isMoving)
        return;

    var key = WinFormsKeyboardAdapter.GetKeyFromString(args.KeyConfig.Key);
    int newX = _gridX;
    int newY = _gridY;

    switch (key)
    {
        case Keys.Left:  newX--; break;
        case Keys.Right: newX++; break;
        case Keys.Up:    newY--; break;
        case Keys.Down:  newY++; break;
        default: return;
    }

    if (newX < 0 || newX >= Columns || newY < 0 || newY >= Rows)
        return;

    _gridX = newX;
    _gridY = newY;
    _isMoving = true;

    _sprite.Movement.ScriptedMovementStopped += OnMovementStopped;
    _sprite.Movement.MoveTo(new(_gridX, _gridY), 0.15f, EasingKind.SmootherStep, 0f);
}

private void OnMovementStopped(Gondwana.Movement.Scripted.ScriptedMovement _)
{
    _sprite.Movement.ScriptedMovementStopped -= OnMovementStopped;
    _isMoving = false;
}
```

---

### Step 8 — Run it

```bash
dotnet run
```

Same result: an 8×8 grid with a bubble you can glide around with the arrow keys.

</details>

---

## What the Spot Demo adds

Spot (`Demos/Spot/`) is a direct extension of everything you just built:

| Spot feature | The extra pieces it uses |
|---|---|
| Two-to-four players | `Player[]` array; `SpotGame.NewGame()` handles multi-player turn order |
| Clone / Jump moves | `MovementType` enum + distance logic in `SpotGameField.GetMovementType()` |
| Cell capture | `SpotGameField.CaptureAdjacentCells()` run after each move lands |
| AI opponent | `SpotGameField.GetBestMovesForPlayer()` + a `Gondwana.Timers.Timer` for the delay |
| Particle effects | `ParticleEmitter` / `ParticleSurface` configured in `CreateDirectDrawings()` |
| Score HUD | `TextBlock` + `DirectRectangle` created in `CreateDirectDrawings()` |
| Background music | `Engine.Managers.AudioResources.LoadFromFile()` + `audioResource.Play()` |
| Sprite animations (jiggle, pulse) | `sprite.StartJiggle()`, `sprite.PulseBy()`, `sprite.ResizeTo()` |

Read `SpotGameHost.cs` in full for a working example of every one of these.

---

## Key mental model

```
GameHostBase
  └─ Engine (singleton, background loop)
       ├─ Scene
       │    └─ SceneLayer (grid + parallax + RefreshQueue)
       │         └─ SceneLayerTile → Sprite → Tilesheet frame
       ├─ ViewManager
       │    └─ View (Camera + Viewport)  ← world↔screen math
       ├─ DirectDrawingManager           ← HUD overlays
       └─ Input (Keyboard / Mouse)
```

The engine renders only what changed each frame via a **dirty-region queue** (`RefreshQueue`). Moving a sprite enqueues the old and new cell rects; nothing else is redrawn. This design keeps the renderer fast and predictable as your game grows.

---

## Further reading

- **Engine Architecture Overview** — [GitHub Wiki](https://github.com/Isthimius/Gondwana/wiki/Engine-Architecture-Overview)
- **Contributor onboarding** — [ONBOARDING.md](https://github.com/Isthimius/Gondwana/blob/master/ONBOARDING.md)
- **API reference** — https://isthimius.github.io/Gondwana/
- **Wiki guides** — https://github.com/Isthimius/Gondwana/wiki


---

A recommended reading order for getting up to speed on the Gondwana engine codebase quickly.

---

## 🗺️ Reading Order

### 1. Get the Big Picture First

**`README.md`** — Read the whole thing (~5 min). It explains the engine's philosophy (dirty-region rendering, world-space first, code-first design), the runtime flow, and lists all namespaces with their responsibilities. This is the best single document in the repo.

---

### 2. Understand the Host/Lifecycle Contract

**`Gondwana.Hosting/GameHostBase.cs`** — The abstract base class for all games. Only ~260 lines, well-documented, and shows the exact lifecycle of a game:

```
Initialize()
  → ConfigureLogging → InitializeEngine → ConfigurePlatform → ConfigureInput
  → LoadContent → CreateSceneGraph → BindScene → InitializeSceneObjects
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

---

# Additional Generated Wiki Articles

## Scenes and SceneLayers
A Scene is Gondwana’s top-level world container. It owns a set of SceneLayer instances, tracks global collision groups, and acts as the unit a render surface binds to when the engine draws a frame.

A SceneLayer is where most playable world content actually lives. Each layer has:
- its own tile grid
- its own coordinate system
- its own parallax factor
- its own dirty-region RefreshQueue
- its own collision registry and resolver
- its own visibility and z-order

This is one of Gondwana’s most important architectural choices: the engine does not treat “the world” as a single flat canvas. Instead, it treats the world as a stack of independently managed layers.

### Why that matters
Because each SceneLayer tracks refresh state independently, Gondwana can redraw only the layers and world regions that changed. That makes layered backgrounds, gameplay layers, collision-debug overlays, and HUD-style scene content practical without forcing a full-frame redraw every cycle.

### Mental model
Think of a Scene as a folder, and each SceneLayer as a transparent sheet inside it:
- some layers move slower (Parallax < 1)
- some move normally (Parallax = 1)
- some can move faster (Parallax > 1)
- each can use a different projection model
- each can be hidden, reordered, shifted, or wrapped

### Key details
- Scene.VisibleSceneLayers is cached and sorted by ascending ZOrder
- structural layer changes mark Scene.FullRefreshNeeded = true
- layer origin is in world pixels
- tile addressing is still layer-local
- collision groups live at scene level, but layers expose them as a convenience

### Where to read next
- Gondwana/Scenes/Scene.cs
- Gondwana/Scenes/SceneLayer.cs
- Gondwana/Rendering/RefreshQueue.cs

---

## Views, Cameras, and Viewports
A View is Gondwana’s answer to the question: what part of the world is being shown, and where is it being shown on screen?

A view combines two things:
- a Camera — what world-space region you are looking at
- a Viewport — where that image appears on the render surface

This separation is deliberate. The camera moves through world space. The viewport does not. The viewport only defines a screen-space rectangle and zoom behavior.

### Camera
The Camera stores a world-space upper-left position in pixels. It can:
- snap instantly
- center on points or tiles
- smoothly follow a target
- pan over time
- clamp itself to world bounds
- use a dead zone for less twitchy follow behavior

When the camera moves, Gondwana marks the scene for a full refresh. That is expected: changing the camera changes what every visible layer should look like.

### Viewport
The Viewport defines:
- target screen rectangle
- zoom level
- optional screen offset
- derived visible world size

Viewport zoom is animated independently from camera movement. This makes “zoom toward cursor” and cinematic pan/zoom behavior possible without mixing concerns.

### Why View matters
Because rendering flows through views, multiple cameras are not a bolt-on feature. Split-screen, picture-in-picture, minimaps, and layered overlays are natural outcomes of the model.

### Mental model
- Camera = “where am I looking in the world?”
- Viewport = “where does that view appear on screen?”
- View = both, together

### Where to read next
- Gondwana/Rendering/Views/View.cs
- Gondwana/Rendering/Views/Camera.cs
- Gondwana/Rendering/Views/Viewport.cs
- Gondwana/Rendering/Views/ViewManager.cs

---

## Coordinate Spaces
Gondwana uses several coordinate spaces on purpose, and understanding them early will save you a lot of confusion.

### 1. Grid space
Grid space is tile-relative. A position like (4, 7) means “column 4, row 7” on a given SceneLayer.

This is useful for:
- tile addressing
- map logic
- spawn placement
- coordinate-system-aware movement

### 2. World pixel space
World space is Gondwana’s primary simulation space.

Most core engine logic happens here:
- camera position
- dirty rectangles
- sprite placement
- collision bounds
- layer origins
- view transforms

This is the engine’s real “source of truth.”

### 3. Screen space
Screen space is render-surface space. These are the pixel coordinates on the destination surface after camera, parallax, viewport placement, and zoom have been applied.

This is where:
- the backbuffer is actually drawn
- dirty presentation regions are tracked
- view overlays live

### 4. Layer-local projection space
Each SceneLayer has a coordinate system implementation that converts grid positions to world pixels. Orthogonal, isometric, and hex variants are handled at this layer.

That means grid-to-world math is layer-specific, not global.

### Key insight
Gameplay logic should usually think in world or grid space. Rendering code is where world becomes screen.

### Mental model
Grid -> World -> Screen

That pipeline is everywhere in Gondwana.

### Where to read next
- Gondwana/Scenes/SceneLayer.cs
- Gondwana/Rendering/Views/View.cs
- coordinate classes under Gondwana/Drawing/Coordinates/

---

## Rendering Pipeline
Gondwana’s render path is intentionally explicit. It is not “draw everything every frame and hope the GPU is fast enough.” On bitmap backbuffers especially, it is built around dirty-region redraw.

### High-level flow
Each engine cycle does two broad things:

#### Background work
- pre-cycle timers
- input polling
- animation advancement
- sprite movement
- collision resolution
- camera updates

#### Foreground work
- direct drawing updates
- render visible views into backbuffers
- present backbuffers to adapters
- post-cycle timers

### Render path on bitmap backbuffers
1. If the scene needs a full refresh, visible world regions are enqueued for each layer
2. Each view collects dirty world rectangles and converts them to screen-space rectangles
3. Dirty screen areas are pre-cleared
4. Each visible layer redraws only drawables intersecting those dirty world regions
5. View-level direct drawings are rendered on top
6. Backbuffer dirty screen area is presented to the platform adapter

### Render path on GPU backbuffers
GPU surfaces currently take a different path: they redraw the full viewport each GL paint. This avoids cross-thread refresh-queue races and fits the GL-thread ownership model better.

### Why this design works
The engine separates:
- what changed (RefreshQueue)
- what is visible (View)
- what gets drawn (Backbuffer)
- how it reaches the screen (RenderSurfaceAdapterBase)

That keeps the core engine predictable and platform-agnostic.

### Where to read next
- Gondwana/Engine.cs
- Gondwana/Rendering/RenderSurfaceHost.cs
- Gondwana/Rendering/Backbuffers/

---

## Backbuffers
A backbuffer is the in-memory drawing surface Gondwana renders into before anything is presented to the screen.

The engine supports two main backbuffer types:
- BitmapBackbuffer
- GpuBackbuffer

Both inherit from BackbufferBase, so the higher-level render pipeline can stay mostly the same.

### BitmapBackbuffer
This is the classic CPU-rendered path.

It uses an in-memory SKBitmap + SKSurface, supports deferred resize requests, and is designed around dirty-rectangle rendering and safe snapshotting for UI presentation.

This is the backbuffer that gets the most benefit from Gondwana’s dirty-region system.

### GpuBackbuffer
This is the GL-thread-rendered path.

Instead of rendering on the engine’s normal foreground loop and then copying to the adapter, GPU surfaces render directly on the GL thread into a GPU render target. The engine treats these differently because partial dirty-rectangle presentation is not the main optimization there.

### Shared responsibilities
A backbuffer is responsible for:
- exposing an SKCanvas
- beginning and ending a frame
- drawing tiles and drawables
- tracking dirty screen pixels
- snapshotting rendered output
- resizing when needed

### Mental model
The backbuffer is not the window. It is the engine’s working canvas.

### Where to read next
- Gondwana/Rendering/Backbuffers/BackbufferBase.cs
- Gondwana/Rendering/Backbuffers/BitmapBackbuffer.cs
- Gondwana/Rendering/Backbuffers/GpuBackbuffer.cs

---

## Refresh Queues
A RefreshQueue is Gondwana’s core dirty-region data structure.

Each SceneLayer owns one. It stores world-space rectangles that need to be redrawn.

That detail matters: refresh queues are not screen-space and not adapter-space. They are world-space first.

### What goes into a RefreshQueue
Whenever something changes visually in a layer, the engine can enqueue a world-pixel rectangle representing the affected area.

Examples include:
- sprite motion
- tile changes
- layer-origin changes
- redraw requests caused by camera/view changes
- screen-space overlay invalidation projected back into world space

### What happens next
Later, during rendering, each View converts those world rects into screen-space dirty rectangles for that particular camera/viewport/parallax combination.

That is the key design win: one world-space change can be projected differently by different views.

### Important behavior
- containment checks prevent storing redundant rects
- thread hops are marshaled back to the engine thread
- rendering consumes snapshots of the queue
- queues are cleared after the frame is rendered

### Mental model
RefreshQueue answers: what changed in the world?
The view answers: where does that appear on screen?

### Where to read next
- Gondwana/Rendering/RefreshQueue.cs
- Gondwana/Rendering/RenderSurfaceHost.cs

---

## Dirty Rectangles
Dirty rectangles are Gondwana’s redraw currency.

But there are two different kinds, and confusing them causes bugs:
- world dirty rectangles — stored in RefreshQueue
- screen dirty rectangles — tracked by the backbuffer for presentation

### World dirty rectangles
These say: “this part of the world needs to be rerendered.”

They are layer-relative in the sense that each layer owns its own queue, but the rectangles themselves are stored in world pixels.

### Screen dirty rectangles
These say: “this part of the render surface changed and needs to be presented.”

These live on the backbuffer and are always in adapter/control screen pixels.

### Why Gondwana separates them
Because a single world change can project into different screen rectangles depending on:
- camera position
- viewport placement
- zoom
- parallax
- overlapping multi-view layouts

That separation is what makes the view-centric model work cleanly.

### Practical takeaway
If you are invalidating scene content, think in world rects.
If you are presenting to the UI adapter, think in screen rects.

### Where to read next
- Gondwana/Rendering/RefreshQueue.cs
- Gondwana/Rendering/Views/View.cs
- Gondwana/Rendering/Backbuffers/BackbufferBase.cs

---

## Parallax and Multi-View Rendering
Parallax in Gondwana is not a special effect bolted onto the renderer. It is a first-class property of SceneLayer.

Each layer has a Parallax factor:
- < 1 moves slower than the camera
- = 1 moves normally
- > 1 moves faster than the camera

Because view transforms apply parallax during world-to-screen conversion, the effect falls out of the normal render path.

### Multi-view rendering
A RenderSurfaceHost can own multiple views. Each one can have:
- its own camera
- its own viewport rectangle
- its own zoom
- its own z-order

This supports:
- split-screen
- minimaps
- picture-in-picture
- layered overlay views

### Overlap behavior
Views are rendered in ascending ZOrder. Higher views can clip lower ones where their viewport rectangles overlap.

That gives Gondwana deterministic compositing even when views share screen space.

### Key insight
Parallax is layer-relative.
Multi-view is render-surface-relative.

Those are different axes of composition, and Gondwana supports both at once.

### Where to read next
- Gondwana/Scenes/SceneLayer.cs
- Gondwana/Rendering/Views/View.cs
- Gondwana/Rendering/Views/ViewManager.cs
- Gondwana/Rendering/RenderSurfaceHost.cs

---

## DirectDrawing
DirectDrawing is Gondwana’s system for drawables that are not just tiles in a layer grid.

This includes things like:
- images
- rectangles
- text
- particles
- overlays attached to a view
- world-space decorative or debug elements

### Two important modes
A direct drawing can be associated with:
- a SceneLayer — world-space drawing
- a View — screen-space overlay drawing

That distinction is extremely useful.

Layer-bound direct drawings behave like scene content.
View-bound direct drawings behave like HUD or overlay content.

### DirectDrawingManager
All direct drawings are centrally managed by DirectDrawingManager, which:
- auto-registers drawings
- updates them each frame
- filters them by layer or view
- sorts them deterministically by ZOrder and name

### Why it exists
Not everything in a game world is best represented as a tile or sprite. DirectDrawing is the escape hatch that is still engine-native.

### Mental model
If sprites are “engine-managed world actors,” direct drawings are “engine-managed custom visuals.”

### Where to read next
- Gondwana/Drawing/Direct/DirectDrawingManager.cs
- Gondwana/Drawing/Direct/

---

## Movement and Controllers
Gondwana’s movement system centers on MovementController, which manages how a movable object changes position over time.

It supports three movement styles:
- Follow
- Scripted
- Integrated

### Follow
Follow mode keeps an object tracking a target, either hard or smoothly. This is useful for camera-like motion, companion motion, or “move toward a live point of interest” behavior.

### Scripted
Scripted movement is for explicit motion plans:
- tweening
- move-toward behaviors
- time-based motion sequences

This is the right fit for cutscenes, UI-like motion, and authored transitions.

### Integrated
Integrated movement is the more physics-like mode. Velocity and acceleration are advanced over time, making it suitable for ordinary gameplay motion.

### Priority model
Each frame, the controller resolves movement in this order:
1. follow
2. scripted
3. integrated

That means authored or tracking motion can temporarily “own” the frame before free integration runs.

### Why this is nice
The system gives you one controller abstraction instead of making you choose between completely separate movement subsystems.

### Where to read next
- Gondwana/Movement/MovementController.cs
- Gondwana/Movement/MovementController.Follow.cs
- Gondwana/Movement/MovementController.Scripted.cs
- Gondwana/Movement/MovementController.Integrated.cs

---

## Input Handling
Gondwana treats input as a polling problem, not a platform-event problem.

Platform adapters feed raw device state into engine pollers, and the engine checks those pollers during its background task phase.

### Input systems
The engine exposes centralized access to:
- keyboard
- mouse
- gamepad
- touch
through EngineInputSystems.

### Engine cycle placement
Input is polled during DoBackgroundTasks, before rendering. That means input changes can affect movement, animation, or scene state before the next frame is drawn.

### Why polling fits Gondwana
Because the engine wants deterministic update flow:
- timer events
- input polling
- movement
- collisions
- camera updates
- rendering

Polling keeps those steps ordered and predictable.

### Platform boundary
The core engine does not want to know about WinForms, Avalonia, SDL2, or browser specifics. Those details stay in adapters. The engine only consumes normalized input state.

### Where to read next
- Gondwana/EngineInputSystems.cs
- Gondwana/Input/Keyboard/KeyboardEventPoller.cs
- Gondwana/Input/Mouse/MouseEventPoller.cs
- Gondwana/Input/Gamepad/GamepadEventPoller.cs
- Gondwana/Input/Touch/TouchEventPoller.cs

---

## Collision Detection
Gondwana’s collision system is scene-oriented and AABB-based.

Each Scene owns collision groups, and each SceneLayer exposes a collider registry and a collision resolver.

### Resolution model
The current resolver is intentionally simple and practical:
- broad-phase query against the registry
- overlap detection in world pixel space
- trigger overlaps reported without push-out
- solid-vs-solid collisions resolved by minimum-axis push-out
- blocked velocity components are canceled so motion can slide on the free axis

### Why this fits the engine
Gondwana is a 2.5D rendering engine with strong tile/sprite assumptions. AABB collision is a good match for that style of game logic and keeps the runtime behavior understandable.

### Important detail
Collision resolution runs after movement in the engine cycle. That means movers advance first, then get corrected if they overlap.

### Mental model
Movement produces a proposed position.
Collision resolution decides whether that position is legal.

### Where to read next
- Gondwana/Collisions/CollisionResolver.cs
- Gondwana/Collisions/
- Gondwana/Scenes/SceneLayer.cs

---

## Timing and Ticks
Gondwana uses a high-resolution tick-based timing model.

Real elapsed time is measured with HighResTimer, while recurring or one-shot scheduled behavior is exposed through Timer.

### Engine timing flow
Every engine cycle computes elapsed time and uses that to drive:
- timer events
- animation advancement
- sprite movement
- camera updates
- CPS/FPS sampling

### Timers
Timers can be:
- pre-cycle
- post-cycle
- once
- repeating

This lets gameplay code schedule work at predictable points in the cycle.

### Why ticks matter
Ticks are the engine’s low-level clock. Seconds are usually derived from them, but the underlying representation stays high-resolution and stable.

### Performance metrics
The engine also samples gross CPS and rendered FPS. Those numbers are useful when diagnosing whether you are CPU-bound, render-bound, or simply not invalidating enough to repaint frequently.

### Where to read next
- Gondwana/Timers/HighResTimer.cs
- Gondwana/Timers/Timer.cs
- Gondwana/Engine.cs

---

## Serialization and EngineState
EngineState is Gondwana’s snapshot-and-restore mechanism for runtime state.

It is not just “save some JSON.” It is a structured capture of the engine’s live registries and content collections.

### What it can include
Depending on selected EngineStateParts, it can save/load:
- asset files
- tilesheets
- animation cycles
- scenes
- sprites
- audio resources

### Important behavior
LoadFromFile clears current state first.
MergeFromFile does not.

That distinction matters:
- use load for full restore
- use merge for patching or layering in content

### Compression
State can optionally be written through GZip, which is useful when snapshots get large.

### Design note
Most stateful engine systems are registry-backed, so EngineState works more like a snapshot facade over live engine collections than a totally isolated save object.

### Where to read next
- Gondwana/EngineState.cs
- Gondwana/EngineStateParts.cs

---

## Custom Render Surfaces
Gondwana’s rendering edge is intentionally abstracted behind two concepts:
- RenderSurfaceHostBase
- RenderSurfaceAdapterBase

If you want a new host platform or a specialized presentation target, this is where you start.

### Host vs adapter
The host owns:
- the scene binding
- the view manager
- the backbuffer
- render orchestration

The adapter owns:
- surface width/height
- resize notifications
- presenting an SKImage to the actual UI surface

### Why that split exists
The engine wants to stay platform-agnostic. It should not care whether the destination is:
- WinForms
- Avalonia
- Blazor canvas interop
- some future custom control

As long as an adapter can present rendered output and report size changes, the engine can do the rest.

### Practical extension points
To add a custom render surface, you typically need:
- a RenderSurfaceAdapterBase implementation
- a compatible host/backbuffer pairing
- resize wiring
- presentation logic

### Where to read next
- Gondwana/Rendering/RenderSurfaceHostBase.cs
- Gondwana/Rendering/RenderSurfaceAdapterBase.cs
- Gondwana/Rendering/RenderSurfaceHost.cs
- platform implementations under Gondwana.WinForms/Rendering/, Gondwana.Avalonia/Rendering/, and Gondwana.Blazor/Rendering/

---

## Performance Tuning
Gondwana already gives you a lot of leverage if you follow its intended model.

The biggest performance win is simple: preserve the dirty-region model whenever possible.

### Good performance habits
- prefer small targeted world invalidations over full-scene refreshes
- avoid unnecessary camera or viewport churn
- keep parallax layers cleanly separated
- use bitmap backbuffers when dirty-region redraw is the goal
- use GPU backbuffers when full-viewport redraw is acceptable and presentation overhead matters more

### Things that force more work
These commonly trigger full refresh behavior:
- camera motion
- viewport resize
- viewport zoom changes
- layer z-order changes
- layer parallax changes
- layer origin changes
- visibility toggles
- debug overlays like grid lines or collision boxes

### Watch the right metrics
If you are tuning, pay attention to:
- how often FullRefreshNeeded is set
- how large refresh queues get
- how large the backbuffer dirty rectangle becomes
- CPS/FPS sampling results

### Practical advice
If performance is poor, first ask:
1. am I invalidating too much?
2. am I moving the camera every cycle?
3. am I on the right backbuffer type?
4. am I drawing too many overlays as view-level direct drawings?

### Where to read next
- Gondwana/Rendering/RefreshQueue.cs
- Gondwana/Rendering/RenderSurfaceHost.cs
- Gondwana/Rendering/Backbuffers/BackbufferBase.cs
- Gondwana/Engine.cs

---

## Debugging and Instrumentation
Gondwana is built to be inspectable.

It exposes lifecycle events, logging infrastructure, debug overlays, and runtime sampling hooks that make it much easier to understand what the engine is doing.

### Useful built-in hooks
The engine exposes events such as:
- PreInitialization
- PostInitialization
- InitializationComplete
- BeforeBackgroundTasksExecute
- AfterBackgroundTasksExecute
- BeforeFrameRender
- AfterFrameRender
- CPSCalculated

These are excellent places to attach diagnostics.

### Visual debugging
At the layer level you can turn on:
- ShowGridLines
- ShowCollisionBoxes

Those are simple but very effective when debugging alignment, tile boundaries, or collision issues.

### Logging
The engine routes logs through EngineLogger, so subsystems can emit structured logs without being tied to one UI or one host.

### Rendering diagnostics mindset
When debugging rendering, ask:
- is the scene actually dirty?
- is the change in world space or screen space?
- did the layer enqueue a refresh rect?
- did the view project it where I expect?
- did the backbuffer mark the screen region dirty for presentation?

That sequence usually gets you to the answer quickly.

### Where to read next
- Gondwana/Engine.cs
- Gondwana/Logging/EngineLogger.cs
- Gondwana/Scenes/SceneLayer.cs
- Gondwana/Rendering/Backbuffers/BackbufferBase.cs
