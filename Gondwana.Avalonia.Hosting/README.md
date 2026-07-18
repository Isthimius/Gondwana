# Gondwana.Avalonia.Hosting

**Gondwana.Avalonia.Hosting** provides the `AvaloniaGameHost` base class, which wires the
Gondwana engine lifecycle into an Avalonia application.

It is the Avalonia equivalent of `Gondwana.WinForms.Hosting`.

## Features

- `AvaloniaGameHost` – abstract base class handling engine init, input adapters, scene binding,
  and lifecycle management
- Works across all Avalonia targets: desktop, WebAssembly, Android, iOS, macOS

## Installation

```bash
dotnet add package Gondwana.Avalonia.Hosting
```

## Usage

```csharp
public class MyGameHost : AvaloniaGameHost
{
    public MyGameHost(AvaloniaBitmapRenderSurfaceControl surface) : base(surface) { }

    protected override Scene CreateInitialScene() => new MyGameScene();
    protected override void CreateSprites() { /* populate scene */ }
}
```

Then in your Avalonia `Window.OnOpened`:

```csharp
_host = new MyGameHost(renderSurface);
_host.Initialize();
```

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Avalonia.Hosting/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine
-   `Gondwana.Avalonia` --- Avalonia rendering and input adapters
-   `Gondwana.Hosting` --- Standard platform-agnostic scaffolding for initializing and running Gondwana games
-   `Gondwana.Widgets` --- UI widget library for creating in-game menus, HUDs, and overlays

## License

MIT
