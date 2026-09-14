# Gondwana.Audio.Midi

**Gondwana.Audio.Midi** adds MIDI playback to the Gondwana Game Engine using MeltySynth and the `Gondwana.Audio.NAudio` backend.

It synthesizes `.mid` / `.midi` files with an embedded SoundFont and registers those formats with the NAudio reader registry so MIDI resources can be loaded through the normal `AudioResourceManager` API.

## Features

- MIDI playback (`.mid`, `.midi`)
- embedded General MIDI SoundFont
- software synthesis through MeltySynth
- integration with `Gondwana.Audio.NAudio`
- normal Gondwana `AudioResource` playback controls after loading

## Installation

```bash
dotnet add package Gondwana.Audio.Midi
```

`Gondwana.Audio.Midi` depends on `Gondwana.Audio.NAudio`, which provides the Windows audio output pipeline.

## Usage

Configure the NAudio backend and register MIDI readers before loading MIDI resources:

```csharp
Engine.Instance.UseNAudio();
Engine.Instance.InitializeMidiAudioFormats();
```

Then use the common audio manager:

```csharp
var music = Engine.Managers.AudioResources.LoadFromFile(
    "theme",
    "assets/theme.mid");

music.IsLooping = true;
music.Play();
```

## Related packages

- `Gondwana` — core engine and backend-neutral audio contracts
- `Gondwana.Audio.NAudio` — Windows desktop audio backend used by MIDI playback
- `Gondwana.Audio.Browser` — browser/WASM audio backend; MIDI synthesis is not currently provided there

## Documentation

- **Wiki:** https://github.com/Isthimius/Gondwana/wiki/Audio
- **API reference:** https://isthimius.github.io/Gondwana/api/
- **Release history:** https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Audio.Midi/CHANGELOG.md

## License

MIT
