# Gondwana.WinForms.Hosting

**Gondwana.WinForms.Hosting** provides a ready-to-use WinForms game host built on top of Gondwana.Hosting.

It combines engine lifecycle management with WinForms rendering and input into a single, practical entry point.

## Features

- Prebuilt WinForms game host
- Integrated rendering and input setup
- Standardized startup flow
- Minimal boilerplate for desktop games

## Installation

```bash
dotnet add package Gondwana.WinForms.Hosting
```

## Usage

Create a bitmap host with its required WinForms render-surface control:

```csharp
using Gondwana.Scenes;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;

public class MyGameHost : WinFormsGameHost
{
    public MyGameHost(WinFormBitmapRenderSurfaceControl surface) : base(surface) { }

    protected override Scene CreateInitialScene() => Scene.Empty;
}
```

Use `WinFormsGpuGameHost` with `WinFormGpuRenderSurfaceControl` for OpenGL/GPU rendering. The host's `Initialize()` method wires up the engine lifecycle; override content and scene hooks to build your game.

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/api/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.WinForms.Hosting/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine
-   `Gondwana.Hosting` --- Standard platform-agnostic scaffolding for initializing and running Gondwana games
-   `Gondwana.Widgets` --- UI widget library for creating in-game menus, HUDs, and overlays
-   `Gondwana.WinForms` --- WinForms rendering and input adapters

## License

MIT
