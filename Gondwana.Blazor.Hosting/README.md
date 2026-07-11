# Gondwana.Blazor.Hosting

**Gondwana.Blazor.Hosting** provides the `BlazorGameHost` base class, which wires the Gondwana
engine lifecycle into a Blazor WebAssembly application.

It is the Blazor equivalent of `Gondwana.WinForms.Hosting` and `Gondwana.Avalonia.Hosting`.

## Features

- `BlazorGameHost` – abstract base class handling engine init, input adapters, scene binding, and lifecycle management
- Timer-driven engine loop via `PeriodicTimer` for single-threaded Blazor WASM
- Works with both Blazor WebAssembly and Blazor Server

## Installation

```bash
dotnet add package Gondwana.Blazor.Hosting
```

## Usage

```csharp
public class MyGameHost : BlazorGameHost
{
    public MyGameHost(BlazorBitmapRenderSurfaceComponent surface) : base(surface) { }

    protected override Scene CreateInitialScene() => new MyGameScene();
    protected override void CreateSprites() { /* populate scene */ }
}
```

Then in your Blazor page's `OnAfterRenderAsync`:

```csharp
@code {
    private BlazorBitmapRenderSurfaceComponent _surface = null!;
    private MyGameHost? _host;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        _host = new MyGameHost(_surface);
        _host.Initialize();
    }
}
```

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Blazor.Hosting/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine
-   `Gondwana.Audio.Browser` --- Browser-based audio playback support
-   `Gondwana.Blazor` --- Web assembly rendering and input adapters

## License

MIT
