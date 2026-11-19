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

This version builds the same **Wanderer** game in your browser using Blazor WebAssembly, Gondwana's GPU-backed WebGL render surface, and the browser-safe asset handling introduced in PR #575. The finished game has an 8×8 grid, a bubble sprite, and arrow-key movement.

### Blazor — Prerequisites

- A **.NET 10 SDK** and the matching `wasm-tools` workload.
- The Gondwana CLI and templates (install in Step 1 if needed).
- A WebGL-capable browser.
- The [`spot_defaults.png` tilesheet](images/spot_defaults.png): five frames, each 93×96 pixels.

Unlike WinForms, this path can be developed on Windows, macOS, or Linux. Assets live under `wwwroot/assets/` and must be downloaded over HTTP rather than read from desktop filesystem paths.

### Blazor — Step 1: Install and scaffold

Install the CLI and templates, if you have not already:

```bash
dotnet tool install --global Gondwana.Cli
gondwana templates install
```

Create your game:

```bash
gondwana new blazor Wanderer
cd Wanderer
```

`gondwana new blazor` checks the selected .NET 10 SDK and offers to install `wasm-tools` when it is missing. If you use `dotnet new gondwana-blazor -n Wanderer` instead, install the workload manually with `dotnet workload install wasm-tools` under the .NET 10 SDK.

The generated project includes:

| File | Purpose |
|---|---|
| `Wanderer.csproj` | .NET 10 Blazor WebAssembly and Gondwana packages |
| `Program.cs` | Browser bootstrap, base-address `HttpClient`, and audio backend setup |
| `GameRenderSurface.razor` | GPU render surface, responsive 800×600 presentation, asynchronous asset downloads, startup/error UI, and disposal |
| `WandererHost.cs` | `BlazorGpuGameHost` and the synchronous content, scene, sprite, and input hooks |
| `wwwroot/assets/README.txt` | Browser asset placement and loading instructions |

### Blazor — Step 2: Add the tilesheet to web assets

Create or use the generated `wwwroot/assets/` directory and put the image here:

```text
Wanderer/
  wwwroot/
    assets/
      spot_defaults.png
```

**Do not** put the image into a project-level `assets/` directory or add `CopyToOutputDirectory` to the `.csproj`. Blazor serves `wwwroot/` automatically. The corresponding **app-relative URL** is `assets/spot_defaults.png` (not `wwwroot/assets/spot_defaults.png` and not `/assets/spot_defaults.png`). Preserve filename case, especially for deployments to case-sensitive servers.

### Blazor — Step 3: Enable the download before host initialization

Open `GameRenderSurface.razor`. In `DownloadAssetsAsync`, uncomment the PNG entry in its `paths` array:

```csharp
string[] paths =
[
    "assets/spot_defaults.png",
    // "assets/game.gaf", // Alternative packed-asset example (Step 7).
];
```

Leave the rest of the generated component alone. The template already:

1. Downloads all enabled entries via the app-base-address `HttpClient` before constructing `WandererHost`.
2. Passes the resulting `IReadOnlyDictionary<string, byte[]>` into the host constructor.
3. Calls synchronous `Initialize()` only **after** the downloads finish.
4. Maintains the loading/error overlay, cancels pending downloads on navigation, and disposes the host safely.
5. Calls `ConfigureLogicalResolution(LogicalWidth, LogicalHeight)` for the default 800×600 game size.

No custom `OnAfterRenderAsync` implementation or additional JavaScript is needed. The generated `WandererHost` constructor already accepts the downloaded asset dictionary; **do not replace it with a different constructor**.

### Blazor — Step 4: Load the PNG from memory

Open `WandererHost.cs`. Add any missing imports:

```csharp
using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Sprites;
using Gondwana.Physics.Movement.Easing;
```

The generated file already imports `Gondwana.Blazor.Input.Keyboard`, `Gondwana.Drawing.Coordinates`, `Gondwana.Drawing.Tilesheets`, `Gondwana.Input.Keyboard`, and `Gondwana.Scenes`.

Add these fields to `WandererHost`, leaving the generated `_assetBytes` field and constructor intact:

```csharp
private const int Columns = 8;
private const int Rows = 8;
private const int CellPx = 64;

private Tilesheet _bubbleTilesheet = null!;
private Sprite _sprite = null!;
private int _gridX;
private int _gridY;
private bool _isMoving;
```

Replace the body of `LoadTilesheets` with:

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

The bytes were downloaded by `GameRenderSurface.razor`. The tilesheet stream remains open until synchronous decoding completes, and is then disposed. **Do not use `LoadFromImageFile` or make `LoadTilesheets` / `LoadAssets` `async void`.**

### Blazor — Step 5: Create the scene and sprite

The scene and sprite code is the same as WinForms. Replace `CreateInitialScene`:

```csharp
protected override Scene CreateInitialScene()
{
    var scene = new Scene();

    scene.AddLayer(
        columnCount: Columns,
        rowCount: Rows,
        width: CellPx,
        height: CellPx,
        zOrder: 10,
        parallax: 1f,
        coordinateSystem: CoordinateSystemTypes.Orthogonal);

    scene[0].ShowGridLines = true;
    return scene;
}
```

Then replace `CreateSprites`:

```csharp
protected override void CreateSprites()
{
    var layer = Scene![0];
    var frame = new Frame(_bubbleTilesheet, 0, 0);

    _sprite = Engine.Managers.Sprites.CreateSprite(layer, frame);
    _sprite.SetPosition(new(_gridX, _gridY));
    _sprite.RenderSize = new Size(56, 56);
    _sprite.VertAlign = VerticalAlignment.Middle;
    _sprite.Visible = true;
}
```

This places the first blue bubble in the top-left cell. Its 93×96 source frame is rendered at 56×56.

### Blazor — Step 6: Handle browser arrow keys

The **browser uses `BlazorKey`**, not `System.Windows.Forms.Keys`. The template has the right input hooks already; replace `OnKeyboardAdapterInitialized` and `OnKeyDown` with:

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
    if (args.KeyAction != KeyAction.Pressed || _isMoving)
        return;

    int newX = _gridX;
    int newY = _gridY;

    switch (args.KeyConfig.Key)
    {
        case nameof(BlazorKey.ArrowLeft):  newX--; break;
        case nameof(BlazorKey.ArrowRight): newX++; break;
        case nameof(BlazorKey.ArrowUp):    newY--; break;
        case nameof(BlazorKey.ArrowDown):  newY++; break;
        default: return;
    }

    if (newX < 0 || newX >= Columns || newY < 0 || newY >= Rows)
        return;

    _gridX = newX;
    _gridY = newY;
    _isMoving = true;

    _sprite.Movement
        .MoveTo(new(_gridX, _gridY), 0.15f, EasingKind.SmootherStep, 0f)
        .OnComplete(() => _isMoving = false);
}
```

Keep the template's `UnhookEvents` override, which detaches `keyboard.KeyDown -= OnKeyDown` during disposal. The `Pressed` filter makes each key press one movement, and `_isMoving` prevents overlapping moves. If arrow keys do not respond, **click the game canvas to give it focus**.

### Blazor — Step 7 (optional): Load a packed GAF instead

PR #575 also includes a **commented GAF example**. If you have a `game.gaf` package containing an `Image` entry named `spot_defaults.png`, you can use it *instead of* the loose PNG:

1. Place the package at `wwwroot/assets/game.gaf`.
2. In `GameRenderSurface.razor`, enable `"assets/game.gaf"` in `DownloadAssetsAsync` and disable `"assets/spot_defaults.png"` if it is no longer needed.
3. In `WandererHost.LoadTilesheets`, replace the PNG implementation with:

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

The generated host already imports `Gondwana.Assets`. Use **one** loading implementation for the `"bubble"` tilesheet; the examples are alternatives, not two required steps. This example disposes the package after decoding. If you retain an `AssetsFile` across hooks, retain and dispose it with the host instead.

### Blazor — Step 8: Run it

From the `Wanderer` directory:

```bash
dotnet restore
dotnet run
```

Open the localhost URL printed by the development server. You should see the responsive game canvas with an 8×8 grid and a blue bubble; click the canvas, then move with the arrow keys.

For a production WebAssembly build:

```bash
gondwana publish blazor
```

`dotnet publish -f net10.0-browser -c Release` is also supported. The published web files live under `bin/Release/net10.0-browser/publish/wwwroot/`. A static web server is needed to serve them; they are not desktop executables.

If the game shows a loading error, check the browser network panel for a missing `assets/spot_defaults.png` or `assets/game.gaf`. If the build reports `NETSDK1147`, install the matching .NET 10 `wasm-tools` workload. If the browser reports `DllNotFoundException: libSkiaSharp`, check that the project targets `net10.0-browser` and the selected SDK/workload match.

**Next step:** Browse `Demos/Spot.Blazor/` to see a more complete browser game, or compare `Demos/Spot.WinForms/` to see how the same engine model is hosted on desktop.

</details>
