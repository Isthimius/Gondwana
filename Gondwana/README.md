# Gondwana Game Engine

**Gondwana** is a 2D and 2.5D game engine for C# / .NET 8 focused on
tile-based worlds, layered rendering, and practical engine architecture.

It provides fine-grained control over rendering, timing, and scene
composition while remaining lightweight and straightforward to integrate
into .NET applications.

## Features

-   Tile and sprite rendering
-   Layered scenes with z-ordering
-   Parallax support
-   Camera / view system
-   Collision detection
-   Particle effects
-   SkiaSharp-based rendering
-   NAudio-based audio playback
-   Cross-platform architecture

## Installation

``` bash
dotnet add package Gondwana
```

## Documentation

-   **Source Code**\
    https://github.com/isthimius/Gondwana

-   **Architecture & Guides**\
    https://github.com/isthimius/Gondwana/wiki

-   **API Reference (Doxygen)**\
    https://isthimius.github.io/Gondwana/

## Related Packages

-   `Gondwana.Audio.Midi` --- MIDI playback and sequencing support
-   `Gondwana.Hosting` --- Standard platform-agnostic scaffolding for initializing and running Gondwana games
-   `Gondwana.Input.SDL2` --- SDL2-based input handling
-   `Gondwana.Video` --- Video playback support
-   `Gondwana.WinForms` --- WinForms rendering and input adapters
-   `Gondwana.WinForms.Hosting` --- WinForms-specific game host that integrates rendering and input into the Gondwana lifecycle

## License

MIT
