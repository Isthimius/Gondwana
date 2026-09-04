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
- `BlazorKey` enum mapping browser `KeyboardEvent.code` values to integer key codes

## Installation

```bash
dotnet add package Gondwana.Blazor
```

## Usage

Add `<BlazorGpuRenderSurfaceComponent>` to your Blazor page or layout and capture a reference
to it using `@ref`:

```razor
@using Gondwana.Blazor.Rendering

<BlazorGpuRenderSurfaceComponent @ref="_surface" style="width: 800px; height: 600px;" />
```

Then initialize your game host in the component's `OnAfterRenderAsync`:

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

Derive the host from `BlazorGpuGameHost`. The WebGL component uses `SKGLView`'s animation loop as
the single browser `requestAnimationFrame` source. Each WebGL paint callback advances the timer-driven
engine and renders a new scene frame only when Gondwana's foreground cadence requires one; otherwise
it re-presents the current GPU backbuffer. Rendering remains entirely inside the WebGL paint callback
while its GPU context is current.

### Bitmap compatibility path

The original Canvas 2D path remains available. Use `BlazorBitmapRenderSurfaceComponent` with a
host derived from `BlazorGameHost` when CPU-backed rendering or bitmap frame inspection is needed.
The bitmap path retains Gondwana's JavaScript `requestAnimationFrame` loop.

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
-   `Gondwana.Widgets` --- UI widget library for creating in-game menus, HUDs, and overlays

## License

MIT
