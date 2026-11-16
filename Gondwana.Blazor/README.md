# Gondwana.Blazor

**Gondwana.Blazor** provides Blazor adapters for rendering and input, enabling Gondwana games to
run as [ASP.NET Blazor WebAssembly](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
applications with standard cross-platform WASM support.

It mirrors the `Gondwana.Avalonia` package but targets the Blazor component model instead of
Avalonia UI.

## Features

- WebGL rendering through `SKGLView` and `GpuBackbuffer`, with no per-frame pixel transfer to JavaScript
- Preserved bitmap rendering through Canvas 2D for compatibility and diagnostics
- Keyboard input integration via Blazor keyboard events on the canvas element
- Mouse / pointer input integration
- Touch input integration
- Browser `localStorage` persistence through `BrowserLocalStorageEngineConfigurationStore`
- Browser developer-console logging through `BrowserConsoleLoggerProvider`
- External-link launching through `BlazorExternalUriLauncher` for Widgets such as `HyperlinkWidget` and `AboutBox`
- `BlazorKey` enum mapping browser `KeyboardEvent.code` values to integer key codes

## Installation

```bash
dotnet add package Gondwana.Blazor
```

For the game-host lifecycle used below, also reference `Gondwana.Blazor.Hosting`:

```bash
dotnet add package Gondwana.Blazor.Hosting
```

For a new game, start with the [Blazor template](../Tooling/Gondwana.Templates/templates/gondwana-blazor/) so the browser runtime and rendering dependencies are configured together.

## Usage

Add the GPU render surface to a component, inject the JavaScript runtime, and initialize
your host after the first render. Dispose the host when the component is removed:

```razor
@using Gondwana.Blazor.Rendering
@using Microsoft.JSInterop
@inject IJSRuntime JS
@implements IDisposable

<BlazorGpuRenderSurfaceComponent @ref="_surface" style="width: 800px; height: 600px;" />

@code {
    private BlazorGpuRenderSurfaceComponent _surface = null!;
    private MyGameHost? _host;

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _host = new MyGameHost(_surface, JS);
            _host.Initialize();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _host?.Dispose();
        _host = null;
    }
}
```

The generated host's constructor forwards both dependencies:

```csharp
internal MyGameHost(
    BlazorGpuRenderSurfaceComponent renderSurface,
    IJSRuntime jsRuntime)
    : base(renderSurface, jsRuntime)
{
}
```

Derive the host from `BlazorGpuGameHost`. The WebGL component uses `SKGLView`'s animation loop as
the single browser `requestAnimationFrame` source. Each WebGL paint callback advances the timer-driven
engine and renders a new scene frame only when Gondwana's foreground cadence requires one; otherwise
it re-presents the current GPU backbuffer. Rendering remains entirely inside the WebGL paint callback
while its GPU context is current.

### Loading browser assets

Desktop filesystem paths passed to `LoadFromImageFile` do not fetch web assets.
For example, place the quickstart image at `wwwroot/assets/spot_defaults.png`
and request it as `assets/spot_defaults.png`, using forward slashes and matching
filename case. Do not include `wwwroot` in the URL. A relative URL also respects
the application's base path when hosted under a subdirectory.

Inject `HttpClient` into the component (`@inject HttpClient Http`), configured
with the application's base address as in the template. Download the content
before initializing the host. For an image-loading version of the component
above, add a `private bool _disposed;` field and replace its lifecycle methods with:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (!firstRender)
        return;

    var imageBytes = await Http.GetByteArrayAsync("assets/spot_defaults.png");
    if (_disposed)
        return; // The component may have been removed during the download.

    _host = new MyGameHost(_surface, JS, imageBytes);
    _host.Initialize();
}

public void Dispose()
{
    _disposed = true;
    _host?.Dispose();
    _host = null;
}
```

In your generated `MyGameHost`, replace the constructor and `LoadTilesheets`
override with the following, and add the fields:

```csharp
private readonly byte[] _imageBytes;
private Gondwana.Drawing.Tilesheets.Tilesheet _bubbleTilesheet = null!;

internal MyGameHost(
    BlazorGpuRenderSurfaceComponent renderSurface,
    IJSRuntime jsRuntime,
    byte[] imageBytes)
    : base(renderSurface, jsRuntime)
{
    _imageBytes = imageBytes;
}

protected override void LoadTilesheets()
{
    using var stream = new System.IO.MemoryStream(_imageBytes, writable: false);
    _bubbleTilesheet = Engine.Managers.Tilesheets.LoadFromStream("bubble", stream);
    _bubbleTilesheet.DefaultRegion.TileSize = new System.Drawing.Size(93, 96);
}
```

`LoadAssets()` and `LoadTilesheets()` are synchronous hooks: initialization
continues when they return. Await downloads beforehand; do not make these hooks
`async void`. The stream above stays open until the image has been decoded.
Use the loaded tilesheet when creating your scene's sprites.

A packed `.gaf` is optional. For multiple assets, the
[Spot Blazor component](../Demos/Spot.Blazor/Pages/Index.razor) demonstrates downloading
a package, loading it with `AssetsFile.Load(stream)`, and passing the resulting
`AssetsFile` to the host. The host then reads entries from the package during
content loading. Applications should also display download/initialization errors;
Spot includes an example.

### Render resolution

`Engine.Instance.Configuration.RenderScale` establishes the Backbuffer resolution from the first
valid canvas size. Later canvas resizes change presentation only, preserving aspect ratio and
centering the image. Changing RenderScale explicitly establishes a new resolution from the current
canvas dimensions. RenderScalingFilter selects Linear or NearestNeighbor, and Host.PresentationScale
reports the derived fit. Pointer input is normalized into logical ScreenPx before routing; canvas
margins are outside the game area. Existing browser/DPI sizing semantics are preserved.

Linear WebGL scaling uses a scoped GPU texture snapshot because Skia's direct DrawSurface does not
expose sampling options. It performs no CPU pixel transfer. Nearest-neighbor and unscaled presentation
retain the direct-surface path. See [viewport scaling notes](../docs/viewport-scaling.md).

### Bitmap compatibility path

The original Canvas 2D path remains available. Use `BlazorBitmapRenderSurfaceComponent` with a
host derived from `BlazorGameHost` when CPU-backed rendering or bitmap frame inspection is needed.
The bitmap path retains Gondwana's JavaScript `requestAnimationFrame` loop.

### Key codes

Both types are in `Gondwana.Blazor.Input.Keyboard`. Use `BlazorKey.ArrowLeft`,
`ArrowRight`, `ArrowUp`, and `ArrowDown` for the arrow keys, and `BlazorKey.KeyA`
for A; do not use WinForms `Keys` values with the Blazor adapter.

`BlazorKeyboardAdapter` uses `(int)BlazorKey` values as key codes. Use
`BlazorKeyboardAdapter.GetKeyCodeFromString("ArrowLeft")` to resolve a key name at runtime,
or use the `BlazorKey` enum directly:

```csharp
Engine.Instance.Input.KeyboardEventPoller!.StartMonitoringKey(
    (int)BlazorKey.Space, "Jump");
```

Register keys in `OnKeyboardAdapterInitialized()` and subscribe to the core
`KeyboardEventPoller.KeyDown` event. In the example above, `args.KeyConfig.Key`
is the supplied name `"Jump"`; filter on `KeyAction.Pressed` for one action per
press. Unsubscribe in `UnhookEvents()`. The canvas must have focus to receive
keyboard events; the render component requests focus on first render.

### Browser console logging

Blazor game hosts automatically attach `BrowserConsoleLoggerProvider` after Gondwana configures the requested log level. Normal `ILogger` calls therefore appear in the browser developer console while continuing to use Gondwana's logging mode and queue.

For example:

```csharp
Engine.Logger.LogInformation(
    "Renderer initialized at {Width}x{Height}",
    width,
    height);
```

With the default `EngineLoggingMode.Asynchronous`, the game or engine call enqueues the record and returns; the logging worker later invokes the browser-console provider. The provider uses the .NET WebAssembly console bridge rather than the desktop `Microsoft.Extensions.Logging.Console` provider.

Warning, error, and critical records use `Console.Error`; lower levels use `Console.Out`.

### Browser configuration and external links

Use `BrowserLocalStorageEngineConfigurationStore` when the same
`EngineConfiguration` should persist in browser `localStorage` instead of a desktop file:

```csharp
var store = new BrowserLocalStorageEngineConfigurationStore(
    JS,
    "my-game.configuration",
    autoSave: true);

host.InitializeWithConfigurationStore(store);
```

Widgets that open an external URI can use the browser implementation directly:

```csharp
var launcher = new BlazorExternalUriLauncher(JS);
```

### Widget text input

`TextBoxWidget` uses host keyboard codes. For Blazor-hosted text boxes, apply the built-in Blazor mapping:

```csharp
BlazorTextBoxInput.Configure(textBox);
```

This configures the text box's special-key codes and printable-character resolver for `BlazorKey` values without changing the public Blazor keyboard-code contract.

### Touch input

The Blazor game hosts initialize touch input automatically. Subscribe in
`OnTouchAdapterInitialized()`; do not initialize a second adapter there.
For manual engine setup without a game host, call `InitializeBlazorTouchAdapter`:

```csharp
Engine.Instance.InitializeBlazorTouchAdapter(renderSurface);
```

After initialization, the touch system is accessible via `Engine.Instance.Input.TouchEventPoller`.

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/api/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Blazor/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine
-   `Gondwana.Audio.Browser` --- Browser-based audio playback support
-   `Gondwana.Blazor.Hosting` --- Blazor-specific game host that integrates rendering and input into the Gondwana lifecycle
-   `Gondwana.Widgets` --- UI widget library for creating in-game menus, HUDs, and overlays

## License

MIT
