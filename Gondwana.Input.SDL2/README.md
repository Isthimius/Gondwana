# Gondwana.Input.SDL2

**Gondwana.Input.SDL2** provides cross-platform input handling using SDL2 gamepads.

It enables gamepad input across multiple platforms with a consistent API.

## Features

- Gamepad support
- Cross-platform via SDL2
- Designed for real-time game input

## Installation

```bash
dotnet add package Gondwana.Input.SDL2
```

## Requirements

- Native SDL2 library must be available at runtime

## Usage

Register the SDL2 input adapter during initialization:

```csharp
host.Engine.InitializeSdlGamepadManager();
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
- `Gondwana.Hosting` — Engine lifecycle setup

## License

MIT
