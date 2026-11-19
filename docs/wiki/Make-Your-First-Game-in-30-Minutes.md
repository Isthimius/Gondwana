This guide builds a small game called **Wanderer**: a bubble on an 8×8 grid that moves smoothly with the arrow keys. Choose your host platform below. Both tutorials use the same Gondwana scene, sprite, and movement concepts, but asset loading and input differ between desktop and browser.

<details>
<summary><strong>WinForms — Windows desktop (GPU)</strong></summary>

This guide walks you through building a small desktop game using the Gondwana engine. By the end, you will have a running WinForms window, a sprite on a grid, and keyboard-driven movement — all the pieces you need to start building something real.

This version uses the **Gondwana CLI** and the **WinForms GPU backbuffer path**.

The completed example mirrors the structure of the **Spot** demo included in this repository (`Demos/Spot.WinForms/`). Spot is a good next read once you finish this guide.

---

## Contents

- [What You'll Build](#what-youll-build)
- [Prerequisites](#prerequisites)
- [Step 1 — Install the Gondwana CLI and templates](#step-1--install-the-gondwana-cli-and-templates)
- [Step 2 — Scaffold a GPU-backed WinForms project](#step-2--scaffold-a-gpu-backed-winforms-project)
- [Step 3 — Add your sprite](#step-3--add-your-sprite)
- [Step 4 — Confirm the generated GPU host](#step-4--confirm-the-generated-gpu-host)
- [Step 5 — Understand the host lifecycle](#step-5--understand-the-host-lifecycle)
- [Step 6 — Fill in `WandererHost.cs`](#step-6--fill-in-wandererhostcs)
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
| Scripted movement | `MoveTo` and its `OnComplete` callback |
| Clean shutdown | Generated `GameWindow.cs` |

---

## Prerequisites

- Windows (this guide uses WinForms)
- .NET 10 SDK
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
| `WandererHost.cs` | Your generated `WandererHost` class with override stubs |
| `assets/README.txt` | Notes for adding game assets |

Because this guide uses `--backbuffer gpu`, the generated host should use:

```csharp
internal sealed class WandererHost : WinFormsGpuGameHost
{
    internal WandererHost(WinFormGpuRenderSurfaceControl renderSurface)
        : base(renderSurface) { }
}
```

If your generated host instead uses `WinFormsGameHost` and `WinFormBitmapRenderSurfaceControl`, you generated the bitmap path. Re-run the scaffold command with:

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

Open `WandererHost.cs`.

The updated WinForms template gives you a host with many override stubs already in place. For the GPU path, the important part is the base class and constructor:

```csharp
internal sealed class WandererHost : WinFormsGpuGameHost
{
    internal WandererHost(WinFormGpuRenderSurfaceControl renderSurface)
        : base(renderSurface) { }
}
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

`WandererHost` initialization internally follows the sequence shown here:

```text
`fixed` — a non-overridable framework step (non-virtual or sealed in the platform host).
`↪ virtual` — extension point; subclasses may override it.

Initialize                             fixed
  → OnInitializing                     ↪ virtual
  → ConfigureLogging                   fixed
  → ConfigurePlatform                  fixed
        → OnConfigurePlatform          ↪ virtual
  → ConfigureInput                     fixed
        → OnKeyboardAdapterInitialized ↪ virtual
        → OnMouseAdapterInitialized    ↪ virtual
        → OnGamepadManagerInitialized  ↪ virtual
        → OnTouchAdapterInitialized    ↪ virtual
        → OnInputConfigured             fixed (platform host)
  → LoadContent                        fixed
        → LoadAssets                   ↪ virtual
        → LoadTilesheets               ↪ virtual
        → LoadAnimationCycles          ↪ virtual
  → CreateSceneGraph                   fixed
        → CreateInitialScene           ↪ virtual
        → CreateInitialViews           ↪ virtual
        → OnSceneGraphCreated          ↪ virtual
  → BindScene                          fixed
        → OnSceneBound                 ↪ virtual
  → InitializeSceneObjects             fixed
        → CreateSprites                ↪ virtual
        → CreateDirectDrawings         ↪ virtual
  → InitializeEngine                   fixed
        → OnEngineInitialized          ↪ virtual
  → StartEngine                        fixed
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

## Step 6 — Fill in `WandererHost.cs`

Open `WandererHost.cs`.

The generated host already includes several `using` statements. Add any of these that are missing:

```csharp
using System.Drawing;
using System.Windows.Forms;
using Gondwana;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Physics.Movement.Easing;
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

A `Tilesheet` is an image atlas. Its `DefaultRegion.TileSize` specifies the size of each frame. The supplied [spot_defaults.png](images/spot_defaults.png) contains five 93×96 frames stacked vertically. Place it in your `assets\` directory.

Fill in `LoadTilesheets`:

```csharp
protected override void LoadTilesheets()
{
    _bubbleTilesheet = Engine.Managers.Tilesheets.LoadFromImageFile("bubble", @"assets\spot_defaults.png");
    _bubbleTilesheet.DefaultRegion.TileSize = new Size(93, 96);
}
```

If you substitute another image, set `DefaultRegion.TileSize` to the dimensions of one frame in that image (not necessarily the entire PNG).

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

The sprite uses frame `(0, 0)`—the first, blue bubble—from the five-frame tilesheet.

Also note that the tilesheet's `DefaultRegion.TileSize` is `(93, 96)`, while the sprite's `RenderSize` is `(56, 56)`. Gondwana scales the source frame to the sprite's render size.

---

### Step 6d — Move the sprite with arrow keys

Gondwana's keyboard event poller raises monitored-key events during the engine cycle. Override `OnKeyboardAdapterInitialized` to subscribe after the adapter is ready, then explicitly tell the poller which keys to watch.

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

The host calls this hook during disposal, before it disposes the Engine. Unsubscribe from events registered during initialization.

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

> **Tip:** press `F5` in Visual Studio to get a debugger-attached run. The generated `GameWindow.cs` passes `LogLevel.Warning` to `_host.Initialize(logLevel: ...)`; change that argument to `LogLevel.Debug` for more engine output.

---

## What the Spot Demo adds

Spot (`Demos/Spot.WinForms/`) is a direct extension of everything you just built:

| Spot feature | The extra pieces it uses |
|---|---|
| Two-to-four players | `Player[]` array; `SpotGame.NewGame()` handles multi-player turn order |
| Clone / Jump moves | `MovementType` enum + distance logic in `SpotGameField.GetMovementType()` |
| Cell capture | `SpotGameField.CaptureAdjacentCells()` run after each move lands |
| AI opponent | `SpotGameField.GetBestMovesForPlayer()` + a `Gondwana.Timers.Timer` for the delay |
| Particle effects | `ParticleEmitter` / `ParticleSurface` created by `SpotGameRuntime` during post-splash/gameplay setup |
| Score HUD | `TextBlock` + `DirectRectangle` created by `SpotGameRuntime.CreateTextBlockFields()` when a new game starts |
| Background music | Assets loaded from the packed `.gaf` via `Engine.Managers.AudioResources.LoadFromStream()` and played after the splash |
| Sprite animations | `sprite.StartJiggle()`, `sprite.PulseBy()`, `sprite.ResizeTo()` |

Read `Demos/Spot.WinForms/Hosts/SpotGameHost.cs` and `SpotGameHost.Content.cs` for the WinForms host and assets. Gameplay and presentation live in `Demos/Spot.Shared/Host/` (`SpotGameHost.Input.cs`, `SpotGameHost.Gameplay.cs`, `SpotGameHost.Presentation.cs`, `SpotGameHost.Particles.cs`, and `SpotGameHost.Hud.cs`).

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

On bitmap backbuffers, Gondwana's dirty-region queue limits CPU redraws. On this desktop GPU path, the engine records a `RenderFrameSnapshot` during foreground work and the GL callback replays the newest completed snapshot. The gameplay example is the same on either path.

---

## Further reading

- **Engine Architecture Overview** — [GitHub Wiki](https://github.com/Isthimius/Gondwana/wiki/Engine-Architecture-Overview)
- **API reference** — https://isthimius.github.io/Gondwana/

</details>

<details>
<summary><strong>Blazor — WebAssembly browser (WebGL)</strong></summary>

This guide walks you through building a small browser game using the Gondwana engine. By the end, you will have a running WebAssembly game, a sprite on a grid, and keyboard-driven movement — all the pieces you need to start building something real.

This version uses the **Gondwana CLI** and the **Blazor WebAssembly GPU render path (WebGL)**.

The completed example mirrors the basic gameplay structure of the **Spot** demo included in this repository (`Demos/Spot.Blazor/`). Spot is a good next read once you finish this guide.

---

<a id="blazor-contents"></a>
## Contents

- [What You'll Build](#blazor-what-youll-build)
- [Prerequisites](#blazor-prerequisites)
- [Step 1 — Install the Gondwana CLI and templates](#blazor-step-1)
- [Step 2 — Scaffold a GPU-backed Blazor project](#blazor-step-2)
- [Step 3 — Add your sprite](#blazor-step-3)
- [Step 4 — Confirm the generated GPU host](#blazor-step-4)
- [Step 5 — Understand the host lifecycle](#blazor-step-5)
- [Step 6 — Fill in `WandererHost.cs`](#blazor-step-6)
- [Step 7 — Run it](#blazor-step-7)
- [What the Spot Demo adds](#blazor-spot-demo)
- [Key mental model](#blazor-key-mental-model)
- [Further reading](#blazor-further-reading)

---

<a id="blazor-what-youll-build"></a>
## What You'll Build

A tiny game called **Wanderer**: a colored bubble sits on an 8×8 grid. The player moves it one cell at a time with the arrow keys. The bubble animates smoothly between cells.

Concepts covered:

| Concept | Where it shows up |
|---|---|
| Project scaffolding | `gondwana new blazor` |
| GPU render path | `BlazorGpuRenderSurfaceComponent` and `BlazorGpuGameHost` |
| Browser asset downloads | `GameRenderSurface.DownloadAssetsAsync` |
| `GameHostBase` lifecycle | Generated host override methods |
| Tilesheets & sprites | `LoadTilesheets` and `CreateSprites` |
| Scene & scene layer | `CreateInitialScene` |
| Keyboard input | `OnKeyboardAdapterInitialized`, `BlazorKey`, and `OnKeyDown` |
| Scripted movement | `MoveTo` and its `OnComplete` callback |
| Clean shutdown | Generated `GameRenderSurface.razor` |

---

<a id="blazor-prerequisites"></a>
## Prerequisites

- A .NET 10 SDK.
- The matching .NET 10 `wasm-tools` workload (the Gondwana CLI can help install it).
- Visual Studio **or** the .NET CLI.
- A WebGL-capable browser.
- An image for your sprite's tilesheet. For this guide you can use the [Spot defaults tilesheet](images/spot_defaults.png).

---

<a id="blazor-step-1"></a>
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

The Blazor project uses the WebAssembly toolchain. `gondwana new blazor` verifies the selected .NET 10 SDK and offers to install `wasm-tools` if it is missing. To check or install the workload yourself:

```bash
dotnet --version
dotnet workload install wasm-tools
dotnet workload list
```

Make sure the SDK selected by `dotnet --version` is a .NET 10 SDK, and that `wasm-tools` appears in the workload list.

---

<a id="blazor-step-2"></a>
## Step 2 — Scaffold a GPU-backed Blazor project

Create a new Blazor WebAssembly Gondwana project:

```bash
gondwana new blazor Wanderer
cd Wanderer
```

The CLI creates the startup shell for you:

| File | What it contains |
|---|---|
| `Wanderer.csproj` | .NET 10 browser target and Gondwana package references |
| `Program.cs` | WebAssembly entry point, browser audio configuration, and app-relative `HttpClient` |
| `GameRenderSurface.razor` | GPU render surface, responsive layout, HTTP asset downloads, and loading/error UI |
| `WandererHost.cs` | Your generated `WandererHost` class with override stubs |
| `wwwroot/assets/README.txt` | Notes for serving game assets from the browser |

This template uses the GPU-backed WebGL path. The generated render component and host should use:

```razor
<BlazorGpuRenderSurfaceComponent @ref="_renderSurface" />
```

```csharp
internal sealed class WandererHost : BlazorGpuGameHost
{
    // The generated constructor also accepts IJSRuntime and downloaded assets.
}
```

You can also create the project directly with `dotnet new gondwana-blazor -n Wanderer`. When using `dotnet new`, ensure you have installed the `wasm-tools` workload yourself.

---

<a id="blazor-step-3"></a>
## Step 3 — Add your sprite

Copy your PNG into the `wwwroot/assets/` subfolder.

For this guide, the sprite is named:

```text
wwwroot/assets/spot_defaults.png
```

Blazor serves the files in `wwwroot/` as static web assets. You do **not** need a `CopyToOutputDirectory` entry in `Wanderer.csproj`.

Open `GameRenderSurface.razor` and find `DownloadAssetsAsync`. Enable the PNG download in the `paths` array:

```csharp
string[] paths =
[
    "assets/spot_defaults.png",
    // "assets/game.gaf", // Optional packed-asset alternative.
];
```

The request path is **`assets/spot_defaults.png`**, relative to the application base URL. Do not include `wwwroot/` or use a leading slash. Use forward slashes and the exact filename case.

The generated component downloads the bytes *before* it creates and initializes the host. It passes the downloaded assets into `WandererHost` for use by synchronous content-loading hooks. The startup screen already reports loading/errors, and navigation cancels pending downloads.

> The template also includes a commented packed `.gaf` loading example. Step 6a shows how to enable it instead of the loose PNG.

---

<a id="blazor-step-4"></a>
## Step 4 — Confirm the generated GPU host

Open `WandererHost.cs`.

The Blazor template gives you a host with several override stubs already in place. The important part is its base class and constructor:

```csharp
internal sealed class WandererHost : BlazorGpuGameHost
{
    private readonly IReadOnlyDictionary<string, byte[]> _assetBytes;

    internal WandererHost(
        BlazorGpuRenderSurfaceComponent renderSurface,
        IJSRuntime jsRuntime,
        IReadOnlyDictionary<string, byte[]> assetBytes)
        : base(renderSurface, jsRuntime)
    {
        _assetBytes = assetBytes;
    }
}
```

This is the browser GPU path. **Keep the generated constructor and `_assetBytes` field**: they receive the image downloaded by `GameRenderSurface.razor`.

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

<a id="blazor-step-5"></a>
## Step 5 — Understand the host lifecycle

The generated `GameRenderSurface.razor` handles browser startup and shutdown for you.

At a high level:

```text
First render
  → download enabled assets from wwwroot via HttpClient
  → create the game host with downloaded bytes
  → initialize the game host
  → configure the logical render resolution

Component disposal / navigation
  → cancel pending downloads
  → dispose the game host
```

The component maintains an 800×600 logical resolution by default (the `LogicalWidth` and `LogicalHeight` constants). It fits that canvas into the available browser viewport while preserving its aspect ratio. You can adjust the constants in `GameRenderSurface.razor` later.

`WandererHost` initialization internally follows the sequence shown here:

```text
`fixed` — a non-overridable framework step.
`↪ virtual` — extension point; subclasses may override it.

Initialize                             fixed
  → OnInitializing                     ↪ virtual
  → ConfigurePlatform                  fixed
        → OnConfigurePlatform          ↪ virtual
  → ConfigureInput                     fixed
        → OnKeyboardAdapterInitialized ↪ virtual
        → OnMouseAdapterInitialized    ↪ virtual
        → OnTouchAdapterInitialized    ↪ virtual
  → LoadContent                        fixed
        → LoadAssets                   ↪ virtual
        → LoadTilesheets               ↪ virtual
        → LoadAnimationCycles          ↪ virtual
  → CreateSceneGraph                   fixed
        → CreateInitialScene           ↪ virtual
        → CreateInitialViews           ↪ virtual
        → OnSceneGraphCreated          ↪ virtual
  → BindScene                          fixed
        → OnSceneBound                 ↪ virtual
  → InitializeSceneObjects             fixed
        → CreateSprites                ↪ virtual
        → CreateDirectDrawings         ↪ virtual
  → InitializeEngine                   fixed
        → OnEngineInitialized          ↪ virtual
  → StartEngine                        fixed
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

Everything else can stay as generated.

**Important:** `LoadAssets` and `LoadTilesheets` are synchronous. Use the bytes already downloaded before `Initialize`; do not make either hook `async void`. The generated loading/error UI and disposal code need no changes.

---

<a id="blazor-step-6"></a>
## Step 6 — Fill in `WandererHost.cs`

Open `WandererHost.cs`.

The generated host already includes several `using` statements. Add any of these that are missing:

```csharp
using System.Drawing;
using Gondwana.Assets;
using Gondwana.Blazor.Hosting;
using Gondwana.Blazor.Input.Keyboard;
using Gondwana.Blazor.Rendering;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Physics.Movement.Easing;
using Gondwana.Scenes;
using Microsoft.JSInterop;
```

Then add these fields near the top of the class, alongside the generated `_assetBytes` field:

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

<a id="blazor-step-6a"></a>
### Step 6a — Load the tilesheet

A `Tilesheet` is an image atlas. Its `DefaultRegion.TileSize` specifies the size of each frame. The supplied [spot_defaults.png](images/spot_defaults.png) contains five 93×96 frames stacked vertically. Place it in your `wwwroot/assets/` directory and enable its URL in `GameRenderSurface.DownloadAssetsAsync` as described in Step 3.

Fill in `LoadTilesheets`:

```csharp
protected override void LoadTilesheets()
{
    using var imageStream =
        new MemoryStream(_assetBytes["assets/spot_defaults.png"], writable: false);

    _bubbleTilesheet =
        Engine.Managers.Tilesheets.LoadFromStream("bubble", imageStream);
    _bubbleTilesheet.DefaultRegion.TileSize = new Size(93, 96);
}
```

The PNG has been downloaded into memory before this hook runs; `LoadFromStream` decodes the tilesheet synchronously. If you substitute another image, set `DefaultRegion.TileSize` to the dimensions of one frame.

**Optional: packed GAF instead of a loose PNG.** Put `game.gaf` in `wwwroot/assets/`. The package must contain an `Image` entry named `spot_defaults.png`. In `GameRenderSurface.DownloadAssetsAsync`, enable `"assets/game.gaf"` and disable the PNG entry. Then use this **alternative** body for `LoadTilesheets`:

```csharp
protected override void LoadTilesheets()
{
    using var packageStream =
        new MemoryStream(_assetBytes["assets/game.gaf"], writable: false);
    using var package = AssetsFile.Load(packageStream);
    using var imageStream =
        package.Get(AssetTypes.Image, "spot_defaults.png")
        ?? throw new InvalidOperationException(
            "Missing Image:spot_defaults.png in game.gaf");

    _bubbleTilesheet =
        Engine.Managers.Tilesheets.LoadFromStream("bubble", imageStream);
    _bubbleTilesheet.DefaultRegion.TileSize = new Size(93, 96);
}
```

Choose **one** implementation for the `"bubble"` tilesheet. Both examples dispose their streams after synchronous decoding; this GAF example also disposes its package. If your game retains a package across content hooks, keep it alive and dispose it when the host shuts down.

---

<a id="blazor-step-6b"></a>
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

<a id="blazor-step-6c"></a>
### Step 6c — Place the sprite

The template gives you `CreateSprites()` for this. Use this method to add your initial sprite:

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

The sprite uses frame `(0, 0)` — the first, blue bubble — from the five-frame tilesheet.

Also note that the tilesheet's `DefaultRegion.TileSize` is `(93, 96)` while the sprite's `RenderSize` is `(56, 56)`. Gondwana scales the source frame to the sprite's render size.

---

<a id="blazor-step-6d"></a>
### Step 6d — Move the sprite with arrow keys

Gondwana's keyboard event poller raises monitored-key events during the engine cycle. Override `OnKeyboardAdapterInitialized` to subscribe after the adapter is ready, then explicitly tell the poller which browser keys to watch.

```csharp
protected override void OnKeyboardAdapterInitialized()
{
    if (Engine.Input.KeyboardEventPoller is not { } keyboard)
        return;

    keyboard.KeyDown += OnKeyDown;

    keyboard.StartMonitoringKey((int)BlazorKey.ArrowLeft, nameof(BlazorKey.ArrowLeft));
    keyboard.StartMonitoringKey((int)BlazorKey.ArrowRight, nameof(BlazorKey.ArrowRight));
    keyboard.StartMonitoringKey((int)BlazorKey.ArrowUp, nameof(BlazorKey.ArrowUp));
    keyboard.StartMonitoringKey((int)BlazorKey.ArrowDown, nameof(BlazorKey.ArrowDown));
}

private void OnKeyDown(KeyDownEventArgs args)
{
    if (args.KeyAction != KeyAction.Pressed)
        return;

    if (_isMoving)
        return;

    int newX = _gridX;
    int newY = _gridY;

    switch (args.KeyConfig.Key)
    {
        case nameof(BlazorKey.ArrowLeft):
            newX--;
            break;

        case nameof(BlazorKey.ArrowRight):
            newX++;
            break;

        case nameof(BlazorKey.ArrowUp):
            newY--;
            break;

        case nameof(BlazorKey.ArrowDown):
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
            0f)                          // snapEpsilon
        .OnComplete(() =>
        {
            _isMoving = false;
        });
}
```

The `_isMoving` guard prevents stacking movement commands while the current animation is still running. `KeyAction.Pressed` limits movement to one action per key press. The game canvas must have keyboard focus; click it if the arrow keys are not responding.

---

<a id="blazor-step-6e"></a>
### Step 6e — Unhook events

The host calls this hook during disposal, before it disposes the Engine. Unsubscribe from events registered during initialization.

```csharp
protected override void UnhookEvents()
{
    if (Engine.Input.KeyboardEventPoller is not null)
        Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;
}
```

Keep `GameRenderSurface.razor`'s generated `Dispose()` implementation too: it cancels any pending asset downloads and disposes the host when the user navigates away.

---

<a id="blazor-step-7"></a>
## Step 7 — Run it

From the project folder:

```bash
gondwana run
```

Or use plain `dotnet`:

```bash
dotnet run
```

Open the local URL reported by the development server. You should see an 8×8 grid with a bubble in the top-left corner. Click the game canvas, then use the arrow keys to glide across the grid.

> **Tip:** The generated `GameRenderSurface.razor` calls `_host.Initialize(logLevel: LogLevel.Warning)`. Change the log level to `LogLevel.Debug` to see more engine messages in the browser developer console.

To publish the browser game:

```bash
gondwana publish blazor
```

Or use plain `dotnet`:

```bash
dotnet publish -f net10.0-browser -c Release
```

The published static web files are under `bin/Release/net10.0-browser/publish/wwwroot/` and must be served by a web server.

If startup fails, check your browser's network panel for missing asset URLs and the console for exceptions. `NETSDK1147` generally indicates a missing `wasm-tools` workload; `DllNotFoundException: libSkiaSharp` warrants checking the selected .NET 10 SDK, `net10.0-browser` target, and matching workload.

---

<a id="blazor-spot-demo"></a>
## What the Spot Demo adds

Spot (`Demos/Spot.Blazor/`) is a direct extension of everything you just built:

| Spot feature | The extra pieces it uses |
|---|---|
| Two-to-four players | `Player[]` array; `SpotGame.NewGame()` handles multi-player turn order |
| Clone / Jump moves | `MovementType` enum + distance logic in `SpotGameField.GetMovementType()` |
| Cell capture | `SpotGameField.CaptureAdjacentCells()` run after each move lands |
| AI opponent | `SpotGameField.GetBestMovesForPlayer()` + engine timers |
| Particle effects | `ParticleEmitter` / `ParticleSurface` during gameplay |
| Score HUD | `TextBlock` and direct drawings |
| Background music | Browser audio resources and the packaged assets |
| Sprite animations | `sprite.StartJiggle()`, `sprite.PulseBy()`, `sprite.ResizeTo()` |
| Browser host | `BlazorGpuGameHost`, WebGL render surface, and asynchronous asset loading |

Read `Demos/Spot.Blazor/SpotGameHost.cs` and `SpotGameHost.Content.cs` for the browser host and content. The browser startup is in `Demos/Spot.Blazor/Pages/Index.razor`; the shared gameplay logic lives under `Demos/Spot.Shared/Host/`.

---

<a id="blazor-key-mental-model"></a>
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

For this guide, the Blazor host uses the **GPU render path**:

```text
BlazorGpuRenderSurfaceComponent
  → BlazorGpuGameHost
  → GpuBackbuffer
  → WebGL-backed Skia surface
```

The game code works with scenes, layers, sprites, and input. `GameRenderSurface.razor` handles the browser's presentation lifecycle: it downloads assets before engine initialization, establishes the logical game resolution, and displays the current GPU-backed frame.

The GPU renderer runs inside the WebGL canvas. The browser's canvas and viewport can resize independently of the game's logical 800×600 resolution, preserving the visible game area and input mapping.

---

<a id="blazor-further-reading"></a>
## Further reading

- **Engine Architecture Overview** — [GitHub Wiki](https://github.com/Isthimius/Gondwana/wiki/Engine-Architecture-Overview)
- **Blazor rendering and input guide** — [Gondwana.Blazor README](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Blazor/README.md)
- **Blazor template** — [Gondwana.Templates](https://github.com/Isthimius/Gondwana/tree/master/Tooling/Gondwana.Templates/templates/gondwana-blazor)
- **API reference** — https://isthimius.github.io/Gondwana/

</details>
