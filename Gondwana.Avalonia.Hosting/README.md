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

-   **Source Code**\
    https://github.com/isthimius/Gondwana

-   **Architecture & Guides**\
    https://github.com/isthimius/Gondwana/wiki

-   **API Reference (Doxygen)**\
    https://isthimius.github.io/Gondwana/

## Related Packages

- `Gondwana` – Core engine
- `Gondwana.Avalonia` – Avalonia platform adapters

## License

MIT
