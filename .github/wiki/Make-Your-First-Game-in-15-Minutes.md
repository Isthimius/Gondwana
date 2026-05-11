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
| `GameWindow.cs` | `Form` with render surface wired to the host lifecycle — identical to Method A, Step 3 |
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

`GameWindow.cs` is already wired correctly (`OnLoad` → `OnShown` → `OnFormClosed`). The same lifecycle diagram applies as in Method A, Step 4 — no changes needed.

---

### Steps 4–7 — Fill in `GameHost.cs`

Open `GameHost.cs`. The scaffolded `WandererGameHost` already has the right base class and empty overrides — add the field declarations and fill in each method with the same logic as Method A.

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

**`LoadTilesheets`** (Method A, Step 5):
```csharp
protected override void LoadTilesheets()
{
    _bubbleTilesheet = new Tilesheet("bubble", @"assets\bubble-blue.png");
    _bubbleTilesheet.TileSize = new System.Drawing.Size(92, 96);
}
```

**`CreateInitialScene`** (Method A, Step 6a):
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

**`CreateSceneGraph`** (Method A, Step 6b):
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

**`OnKeyboardAdapterInitialized`, `UnhookEvents`, and `OnKeyDown`** (Method A, Step 7): copy verbatim — the logic is identical.

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
