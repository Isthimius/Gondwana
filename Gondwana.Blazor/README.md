# Gondwana.Blazor

**Gondwana.Blazor** provides Blazor adapters for rendering and input, enabling Gondwana games to
run as [ASP.NET Blazor WebAssembly](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
applications with standard cross-platform WASM support.

It mirrors the `Gondwana.Avalonia` package but targets the Blazor component model instead of
Avalonia UI.

## Features

- Bitmap rendering surface using a browser `<canvas>` element via the Canvas 2D API (no platform-specific SkiaSharp view package required)
- Keyboard input integration via Blazor keyboard events on the canvas element
- Mouse / pointer input integration
- Touch input integration
- `BlazorKey` enum mapping browser `KeyboardEvent.code` values to integer key codes

## Installation

```bash
dotnet add package Gondwana.Blazor
```

## Usage

Add `<BlazorBitmapRenderSurfaceComponent>` to your Blazor page or layout and capture a reference
to it using `@ref`:

```razor
@using Gondwana.Blazor.Rendering

<BlazorBitmapRenderSurfaceComponent @ref="_surface" style="width: 800px; height: 600px;" />
```

Then initialize your game host in the component's `OnAfterRenderAsync`:

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

### Key codes

`BlazorKeyboardAdapter` uses `(int)BlazorKey` values as key codes. Use
`BlazorKeyboardAdapter.GetKeyCodeFromString("ArrowLeft")` to resolve a key name at runtime,
or use the `BlazorKey` enum directly:

```csharp
Engine.Instance.Input.KeyboardEventPoller!.StartMonitoringKey(
    (int)BlazorKey.Space, "Jump");
```

### Touch input

Enable touch input by calling `InitializeBlazorTouchAdapter`:

```csharp
Engine.Instance.InitializeBlazorTouchAdapter(renderSurface);
```

After initialization, the touch system is accessible via `Engine.Instance.Input.TouchEventPoller`.

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Blazor/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine
-   `Gondwana.Audio.Browser` --- Browser-based audio playback support
-   `Gondwana.Blazor.Hosting` --- Blazor-specific game host that integrates rendering and input into the Gondwana lifecycle

## License

MIT
