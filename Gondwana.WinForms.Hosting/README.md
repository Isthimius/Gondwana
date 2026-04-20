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

Create a host by inheriting from the WinForms host:

```csharp
public class MyGameHost : WinFormsGameHost
{
    protected override void CreateInitialScene()
    {
        // Setup scene
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
- `Gondwana.Hosting` — Base hosting framework
- `Gondwana.WinForms` — Platform adapters

## License

MIT
