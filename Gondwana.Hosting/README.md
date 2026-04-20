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

Create a custom host by inheriting from the base host:

```csharp
public class MyGameHost : GameHostBase
{
    protected override void CreateInitialScene()
    {
        // Setup scene here
    }
}
```

## Documentation

-   **Source Code**\
    https://github.com/isthimius/Gondwana

-   **Architecture & Guides**\
    https://github.com/isthimius/Gondwana/wiki

-   **API Reference (Doxygen)**\
    https://isthimius.github.io/Gondwana/

## Related Packages

- `Gondwana` — Core engine
- `Gondwana.WinForms.Hosting` — WinForms-specific host

## License

MIT
