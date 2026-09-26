This guide walks you through building a small desktop game using the Gondwana engine. By the end, you will have a running WinForms window, a sprite on a grid, and keyboard-driven movement — all the pieces you need to start building something real.

This version uses the **Gondwana CLI** and the **WinForms GPU backbuffer path**.

The completed example mirrors the structure of the **Spot** demo included in this repository (`Demos/Spot/`). Spot is a good next read once you finish this guide.

---

## Contents

- [What You'll Build](#what-youll-build)
- [Prerequisites](#prerequisites)
- [Step 1 — Install the Gondwana CLI and templates](#step-1--install-the-gondwana-cli-and-templates)
- [Step 2 — Scaffold a GPU-backed WinForms project](#step-2--scaffold-a-gpu-backed-winforms-project)
- [Step 3 — Add your sprite](#step-3--add-your-sprite)
- [Step 4 — Confirm the generated GPU host](#step-4--confirm-the-generated-gpu-host)
- [Step 5 — Understand the host lifecycle](#step-5--understand-the-host-lifecycle)
- [Step 6 — Fill in `GameHost.cs`](#step-6--fill-in-gamehostcs)
- [Step 7 — Run it](#step-7--run-it)
- [What the Spot Demo adds](#what-the-spot-demo-adds)
- [Key mental model](#key-mental-model)
- [Further reading](#further-reading)

---

## What You'll Build

A tiny game called **Wanderer**: a colored bubble sits on an 8×8 grid. The player moves it one cell at a time with the arrow keys. The bubble animates smoothly between cells.

Concepts covered:

| Concept | Where it shows up |
|---|---|
| Project scaffolding | `gondwana new winforms` |
| GPU render path | `--backbuffer gpu` |
| `GameHostBase` lifecycle | Generated host override methods |
| Tilesheets & sprites | `LoadTilesheets` and `CreateSprites` |
| Scene & scene layer | `CreateInitialScene` |
| Keyboard input | `OnKeyboardAdapterInitialized` and `OnKeyDown` |
| Scripted movement | `MoveTo` and `ScriptedMovementStopped` |
| Clean shutdown | Generated `GameWindow.cs` |

---

## Prerequisites

- .NET 8 SDK
- Visual Studio 2022 **or** the .NET CLI
- An image for your sprite's Tilesheet. For this guide you can use [Spot defaults tilesheet](images/spot_defaults.png).

---

## Step 1 — Install the Gondwana CLI and templates

Install the CLI as a .NET global tool:

```bash
dotnet tool install --global Gondwana.Cli
```

Install the Gondwana project templates:

```bash
gondwana templates install
```

> You only need to do this once per machine. If the CLI is already installed, use `dotnet tool update --global Gondwana.Cli` when you want the latest version.

---

## Step 2 — Scaffold a GPU-backed WinForms project

Create a new WinForms Gondwana project using the GPU backbuffer:

```bash
gondwana new winforms Wanderer --backbuffer gpu
cd Wanderer
```

You can also use the short option:

```bash
gondwana new winforms Wanderer -b gpu
cd Wanderer
```

The CLI creates the startup shell for you:

| File | What it contains |
|---|---|
| `Wanderer.csproj` | Gondwana package references and asset content examples |
| `Program.cs` | Standard `[STAThread]` WinForms entry point |
| `GameWindow.cs` | WinForms window and render surface wiring |
| `GameHost.cs` | Your generated `WandererGameHost` class with override stubs |
| `assets/README.txt` | Notes for adding game assets |

Because this guide uses `--backbuffer gpu`, the generated host should use:

```csharp
internal sealed class WandererGameHost : WinFormsGpuGameHost
{
    internal WandererGameHost(WinFormGpuRenderSurfaceControl renderSurface)
        : base(renderSurface) { }
}
```

If your generated host instead uses `WinFormsBitmapGameHost` and `WinFormBitmapRenderSurfaceControl`, you generated the bitmap path. Re-run the scaffold command with:

```bash
gondwana new winforms Wanderer --backbuffer gpu
```

---

## Step 3 — Add your sprite

Copy your PNG into the `assets\` subfolder.

For this guide, the sprite is named:

```text
assets\spot_defaults.png
```

Then make sure the file is included as content in `Wanderer.csproj`:

```xml
<ItemGroup>
  <Content Include="assets\spot_defaults.png">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

> The generated `.csproj` may already include a commented-out content example. Use that pattern.

---

## Step 4 — Confirm the generated GPU host

Open `GameHost.cs`.

The updated WinForms template gives you a host with many override stubs already in place. For the GPU path, the important part is the base class and constructor:

```csharp
internal sealed class WandererGameHost : WinFormsGpuGameHost
{
    internal WandererGameHost(WinFormGpuRenderSurfaceControl renderSurface)
        : base(renderSurface) { }
```

That is the GPU path.

You should also see override stubs similar to these:

```csharp
protected override void LoadTilesheets()
{
}

protected override Scene CreateInitialScene()
{
    return Scene.Empty;
}

protected override void CreateSprites()
{
}

protected override void OnKeyboardAdapterInitialized()
{
}

protected override void UnhookEvents()
{
}
```

Those are the main hooks this guide will fill in.

---

## Step 5 — Understand the host lifecycle

The generated `GameWindow.cs` handles the WinForms lifecycle for you.

At a high level:

```text
OnLoad
  → create the game host

OnShown
  → initialize the game host

OnFormClosed
  → dispose the game host
```

`MyGameHost` initialization internally follows the sequence shown here:

```text
`🔒sealed` — fixed framework step; subclasses cannot override it.
`↪ virtual` — extension point; subclasses may override it.

Initialize                             🔒sealed
  → OnInitializing                     ↪ virtual
  → ConfigureLogging                   🔒sealed
  → ConfigurePlatform                  🔒sealed
        → OnConfigurePlatform          ↪ virtual
  → ConfigureInput                     🔒sealed
        → OnKeyboardAdapterInitialized ↪ virtual
        → OnMouseAdapterInitialized    ↪ virtual
        → OnGamepadManagerInitialized  ↪ virtual
        → OnTouchAdapterInitialized    ↪ virtual
  → LoadContent                        🔒sealed
        → LoadAssets                   ↪ virtual
        → LoadTilesheets               ↪ virtual
        → LoadAnimationCycles          ↪ virtual
  → CreateSceneGraph                   🔒sealed
        → CreateInitialScene           ↪ virtual
        → CreateInitialViews           ↪ virtual
        → OnSceneGraphCreated          ↪ virtual
  → BindScene                          🔒sealed
        → OnSceneBound                 ↪ virtual
  → InitializeSceneObjects             🔒sealed
        → CreateSprites                ↪ virtual
        → CreateDirectDrawings         ↪ virtual
  → InitializeEngine                   🔒sealed
        → OnEngineInitialized          ↪ virtual
  → StartEngine                        🔒sealed
        → OnEngineStarted              ↪ virtual
  → OnInitialized                      ↪ virtual
```

For this first game, you only need to fill in a few game-specific methods:

- `LoadTilesheets`
- `CreateInitialScene`
- `CreateSprites`
- `OnKeyboardAdapterInitialized`
- `UnhookEvents`
- the movement handlers

Everything else can stay empty (or removed).

---

## Step 6 — Fill in `GameHost.cs`

Open `GameHost.cs`.

The scaffolded file already includes several `using` statements. Add any of these that are missing:

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
```

Then add these fields near the top of the class:

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

---

### Step 6a — Load the tilesheet

A `Tilesheet` is an image atlas. `TileSize` tells Gondwana how large one frame is. A single-frame image is the simplest possible case. For this guide, you can place [spot_defaults.png](images/spot_defaults.png) in your `assets\` directory.

Fill in `LoadTilesheets`:

```csharp
protected override void LoadTilesheets()
{
    _bubbleTilesheet = Engine.Managers.Tilesheets.LoadFromImageFile("bubble", @"assets\spot_defaults.png");
    _bubbleTilesheet.TileSize = new Size(93, 96);
}
```

If you use a different sprite size, change `TileSize` to match your PNG.

---

### Step 6b — Build the scene

Fill in `CreateInitialScene`:

```csharp
protected override Scene CreateInitialScene()
{
    var scene = new Scene();

    scene.AddLayer(
        columnCount:      Columns,
        rowCount:         Rows,
        width:            CellPx,
        height:           CellPx,
        zOrder:           10,
        parallax:         1f,
        coordinateSystem: CoordinateSystemTypes.Orthogonal);

    scene[0].ShowGridLines = true;

    return scene;
}
```

This creates one 8×8 orthogonal layer. Each cell is 64×64 pixels.

---

### Step 6c — Place the sprite

The template gives you `CreateSprites()` for this. Use this method to add your initial sprite(s):

```csharp
protected override void CreateSprites()
{
    var layer = Scene![0];

    var frame = new Frame(_bubbleTilesheet, 0, 0);

    _sprite = Engine.Managers.Sprites.CreateSprite(layer, frame);
    _sprite.SetPosition(new(_gridX, _gridY));
    _sprite.RenderSize = new Size(56, 56);
    _sprite.VertAlign  = VerticalAlignment.Middle;
    _sprite.Visible    = true;
}
```

The sprite uses frame `(0, 0)` from the tilesheet. Since this guide uses a single image, this is the only frame we need.

Also note that the Sprite's RenderSize `(93, 96)` is different from the Tilesheet's TileSize `(56, 56)`. The engine automatically handles the size scaling.

---

### Step 6d — Move the sprite with arrow keys

Gondwana's keyboard adapter fires events on the engine thread. Override `OnKeyboardAdapterInitialized` to subscribe after the adapter is ready, then explicitly tell it which keys to watch.

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

private void OnKeyDown(KeyDownEventArgs args)
{
    if (args.KeyAction != KeyAction.Pressed)
        return;

    if (_isMoving)
        return;

    var key = WinFormsKeyboardAdapter.GetKeyFromString(
        args.KeyConfig.Key);

    int newX = _gridX;
    int newY = _gridY;

    switch (key)
    {
        case Keys.Left:
            newX--;
            break;

        case Keys.Right:
            newX++;
            break;

        case Keys.Up:
            newY--;
            break;

        case Keys.Down:
            newY++;
            break;

        default:
            return;
    }

    if (newX < 0 || newX >= Columns ||
        newY < 0 || newY >= Rows)
    {
        // do not allow moving off of grid
        return;
    }

    _gridX = newX;
    _gridY = newY;
    _isMoving = true;

    _sprite.Movement
        .MoveTo(
            new(_gridX, _gridY),          // target
            0.15f,                        // seconds
            EasingKind.SmootherStep,      // easingKind
            0f)                           // snapEpsilon
        .OnComplete(() =>
        {
            _isMoving = false;
        });
}
```

The `_isMoving` guard prevents stacking movement commands while the current animation is still running.

---

### Step 6e — Unhook events

This method override is used to unsubscribe explicitly from events when the Engine is disposed.

```csharp
protected override void UnhookEvents()
{
    if (Engine.Input.KeyboardEventPoller is not null)
        Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;
}
```

---

## Step 7 — Run it

From the project folder:

```bash
gondwana run
```

Or use plain `dotnet`:

```bash
dotnet run
```

You should see an 8×8 grid with a bubble in the top-left corner. Arrow keys glide it across the grid.

> **Tip:** press `F5` in Visual Studio to get a debugger-attached run. Gondwana logs at `LogLevel.Warning` by default; bump it to `LogLevel.Debug` in `Initialize()` if you want more engine output.

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
| Sprite animations | `sprite.StartJiggle()`, `sprite.PulseBy()`, `sprite.ResizeTo()` |

Read `Demos/Spot/Hosts/SpotGpuGameHost.cs` together with `SpotGpuGameHost.Game.cs` for a working example of these systems in the full Spot demo.

---

## Key mental model

```text
GameHostBase
  └─ Engine
       ├─ Scene
       │    └─ SceneLayer
       │         └─ SceneLayerTile → Sprite → Tilesheet frame
       ├─ ViewManager
       │    └─ View
       ├─ DirectDrawingManager
       └─ Input
```

For this guide, the WinForms host uses the **GPU render path**:

```text
WinFormGpuRenderSurfaceControl
  → WinFormsGpuGameHost
  → GpuBackbuffer
  → GPU-backed Skia surface
```

The gameplay model is the same whether Gondwana is using the GPU backbuffer path or the bitmap backbuffer path: scenes contain layers, layers contain tiles and sprites, and input changes game state. The difference is presentation. This guide renders through the GPU-backed surface, but the game code itself would look the same when using a bitmap render surface.

On bitmap backbuffers, Gondwana’s dirty-region queue helps optimize presentation because that path is CPU-bound. On the GPU path, presentation is handed off to the GPU-backed Skia surface. Either way, the first-game developer experience is the same: build the scene, move the sprite, and let the selected host present the frame.

---

## Further reading

- **Engine Architecture Overview** — [GitHub Wiki](https://github.com/Isthimius/Gondwana/wiki/Engine-Architecture-Overview)
- **API reference** — https://isthimius.github.io/Gondwana/
