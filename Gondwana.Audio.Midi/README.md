# Gondwana.Audio.Midi

**Gondwana.Audio.Midi** adds MIDI playback support to the Gondwana Game Engine using MeltySynth.

It enables playback of `.mid` / `.midi` files with embedded SoundFont support, making it suitable for lightweight music systems without external dependencies.

## Features

- MIDI file playback (`.mid`, `.midi`)
- Built-in SoundFont support
- Lightweight synthesis via MeltySynth
- Integrates with Gondwana audio system
- No external runtime dependencies

## Installation

```bash
dotnet add package Gondwana.Audio.Midi
```

## Documentation

-   **Source Code**\
    https://github.com/isthimius/Gondwana

-   **Architecture & Guides**\
    https://github.com/isthimius/Gondwana/wiki

-   **API Reference (Doxygen)**\
    https://isthimius.github.io/Gondwana/

## Usage

Register MIDI support through the Gondwana audio system:

```csharp
host.Engine.InitializeMidiAudioFormats();
```

## Related Packages

- `Gondwana` — Core engine
- `Gondwana.Hosting` — Engine bootstrapping and lifecycle

## License

MIT
