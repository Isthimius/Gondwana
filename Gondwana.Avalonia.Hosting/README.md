# Gondwana.Avalonia.Hosting

**Gondwana.Avalonia.Hosting** provides the `AvaloniaBitmapGameHost` and `AvaloniaGpuGameHost` base classes, which wire the Gondwana engine lifecycle into Avalonia applications.

It is the Avalonia equivalent of `Gondwana.WinForms.Hosting`.

## Features

- `AvaloniaBitmapGameHost` – bitmap host with engine initialization, input adapters, scene binding, and lifecycle management
- `AvaloniaGpuGameHost` – OpenGL/GPU host for compatible Windows, Linux, and macOS desktop environments
- Desktop platforms are the documented hosting targets; other Avalonia targets require deployment and rendering validation

## Installation

```bash
dotnet add package Gondwana.Avalonia.Hosting
```

## Usage

```csharp
public class MyGameHost : AvaloniaBitmapGameHost
{
    public MyGameHost(AvaloniaBitmapRenderSurfaceControl surface) : base(surface) { }

    protected override Scene CreateInitialScene() => Scene.Empty;
    protected override void CreateSprites() { /* populate scene */ }
}
```

Then in your Avalonia window's `Opened` event handler:

```csharp
_host = new MyGameHost(renderSurface);
_host.Initialize();
```

Use `AvaloniaGpuGameHost` and `AvaloniaGpuRenderSurfaceControl` for an OpenGL-capable desktop host. The bitmap path does not establish that every mobile or browser target has been tested end to end.

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/api/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Avalonia.Hosting/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine
-   `Gondwana.Avalonia` --- Avalonia rendering and input adapters
-   `Gondwana.Hosting` --- Standard platform-agnostic scaffolding for initializing and running Gondwana games
-   `Gondwana.Widgets` --- UI widget library for creating in-game menus, HUDs, and overlays

## License

MIT
