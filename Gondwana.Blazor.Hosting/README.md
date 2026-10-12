# Gondwana.Blazor.Hosting

**Gondwana.Blazor.Hosting** provides GPU and bitmap game-host base classes that wire the Gondwana
engine lifecycle into a Blazor WebAssembly application.

It is the Blazor equivalent of `Gondwana.WinForms.Hosting` and `Gondwana.Avalonia.Hosting`.

## Features

- `BlazorGpuGameHost` – WebGL/GPU host using `BlazorGpuRenderSurfaceComponent`
- `BlazorGameHost` – preserved Canvas 2D/bitmap host using `BlazorBitmapRenderSurfaceComponent`
- Timer-driven browser engine execution: WebGL uses `SKGLView`'s `requestAnimationFrame` loop directly, while the bitmap path retains Gondwana's JavaScript render loop
- Works with both Blazor WebAssembly and Blazor Server
- `ConfigureLogicalResolution(width, height)` retains game coordinates across browser resizes by handling the first usable canvas size

## Installation

```bash
dotnet add package Gondwana.Blazor.Hosting
```

## Usage

```csharp
public class MyGameHost : BlazorGpuGameHost
{
    public MyGameHost(BlazorGpuRenderSurfaceComponent surface, IJSRuntime jsRuntime)
        : base(surface, jsRuntime) { }

    protected override Scene CreateInitialScene() => new MyGameScene();
    protected override void CreateSprites() { /* populate scene */ }
}
```

Then in your Blazor page's `OnAfterRenderAsync`:

```csharp
@code {
    private BlazorGpuRenderSurfaceComponent _surface = null!;
    private MyGameHost? _host;

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return Task.CompletedTask;
        _host = new MyGameHost(_surface, JS);
        _host.Initialize();
        _host.ConfigureLogicalResolution(800, 600); // Optional fixed game coordinates
        return Task.CompletedTask;
    }
}
```

For the bitmap compatibility path, substitute `BlazorGameHost` and
`BlazorBitmapRenderSurfaceComponent`.

## Responsive browser presentation with stable logical size

Call `ConfigureLogicalResolution(800, 600)` **after engine initialization**
(typically following `Initialize()` in a Blazor page) to preserve a minimum
logical game size. WebGL begins with a 1×1 placeholder; Hosting subscribes
to the canvas adapter and waits for its first usable pixel size. It sets
`Engine.Configuration.RenderScale` **once**, accounting for integer rounding so
the backbuffer does not crop the intended game dimensions. Later viewport
resizing and orientation changes use the engine's existing presentation/input
transforms without changing the logical game size. The subscription is
automatically removed on first success or host disposal.

This is **opt-in** and may be called only once per game host. Do not call it
for a canvas whose logical game size should follow its physical dimensions.

The browser's responsive CSS is separate from the engine's logical resolution.
The [Blazor template](../Tooling/Gondwana.Templates/templates/gondwana-blazor/GameRenderSurface.razor)
uses an aspect-preserving `Fit` layout capped at the configured desktop size.
Its `LogicalWidth` and `LogicalHeight` constants drive **both** the CSS size
and the host API call. To use a fixed-size or stretched layout instead, adjust
the generated container CSS; there is no extra rendering mode to configure.

The template also includes pre-WASM HTML and a matching Blazor initialization
overlay. Those appear before any in-engine `SplashScreen` widget is usable.

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/api/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Blazor.Hosting/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine
-   `Gondwana.Audio.Browser` --- Browser-based audio playback support
-   `Gondwana.Blazor` --- Web assembly rendering and input adapters
-   `Gondwana.Widgets` --- UI widget library for creating in-game menus, HUDs, and overlays

## License

MIT
