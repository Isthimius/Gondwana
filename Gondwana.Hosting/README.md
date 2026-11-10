# Gondwana.Hosting

**Gondwana.Hosting** provides a structured, platform-agnostic way to initialize and run Gondwana applications.

It standardizes engine startup, configuration, asset loading, and lifecycle management so projects start clean and stay maintainable.

## Features

- Engine initialization scaffolding
- Lifecycle hooks (start/stop)
- Structured asset loading
- Input configuration hooks
- Clean separation of setup vs runtime logic

## Installation

```bash
dotnet add package Gondwana.Hosting
```

## Usage

Create a platform-neutral host by inheriting from `GameHostBase` and returning a `Scene` from `CreateInitialScene`:

```csharp
using Gondwana.Hosting;
using Gondwana.Scenes;

public class MyGameHost : GameHostBase
{
    protected override void ConfigurePlatform()
    {
        // Configure the platform-specific render and input services.
    }

    protected override Scene CreateInitialScene() => Scene.Empty;
}
```

A concrete application must also supply render-surface/scene binding. For a ready-made platform integration, derive from the WinForms, Avalonia, or Blazor hosting classes instead.

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/api/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Hosting/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine
-   `Gondwana.Avalonia.Hosting` --- Avalonia-specific game host that integrates rendering and input into the Gondwana lifecycle
-   `Gondwana.Blazor.Hosting` --- Blazor-specific game host that integrates rendering and input into the Gondwana lifecycle
-   `Gondwana.WinForms.Hosting` --- WinForms-specific game host that integrates rendering and input into the Gondwana lifecycle
-   `Gondwana.Widgets` --- UI widget library for creating in-game menus, HUDs, and overlays

## License

MIT
