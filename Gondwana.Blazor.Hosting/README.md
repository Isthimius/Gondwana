# Gondwana.Blazor.Hosting

**Gondwana.Blazor.Hosting** provides GPU and bitmap game-host base classes that wire the Gondwana
engine lifecycle into a Blazor WebAssembly application.

It is the Blazor equivalent of `Gondwana.WinForms.Hosting` and `Gondwana.Avalonia.Hosting`.

## Features

- `BlazorGpuGameHost` – WebGL/GPU host using `BlazorGpuRenderSurfaceComponent`
- `BlazorGameHost` – preserved Canvas 2D/bitmap host using `BlazorBitmapRenderSurfaceComponent`
- Browser-driven engine loop using `requestAnimationFrame` for single-threaded Blazor WASM
- Works with both Blazor WebAssembly and Blazor Server

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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        _host = new MyGameHost(_surface);
        _host.Initialize();
    }
}
```

For the bitmap compatibility path, substitute `BlazorGameHost` and
`BlazorBitmapRenderSurfaceComponent`.

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Blazor.Hosting/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine
-   `Gondwana.Audio.Browser` --- Browser-based audio playback support
-   `Gondwana.Blazor` --- Web assembly rendering and input adapters
-   `Gondwana.Widgets` --- UI widget library for creating in-game menus, HUDs, and overlays

## License

MIT
