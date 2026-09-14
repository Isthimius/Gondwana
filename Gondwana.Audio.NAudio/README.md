# Gondwana.Audio.NAudio

**Gondwana.Audio.NAudio** provides the Windows desktop audio backend for the Gondwana Game Engine using NAudio.

The core `Gondwana` package defines the backend-neutral `Gondwana.Audio` contracts and resource APIs. This package supplies the NAudio implementation used by Windows desktop games.

## Features

- WAV and MP3 playback
- OGG / OGA / MOGG playback through NAudio.Vorbis
- WMA and M4A playback through Media Foundation
- play, pause, resume, stop, seek, looping, volume, and stereo pan
- variable playback speed from 0.25x through 4.0x
- loading from files, streams, and Gondwana asset files
- integration with `Engine.Managers.AudioResources`

Playback speed changes rate and pitch together. Pitch-preserving time stretching is not part of the core audio contract.

## Setup

```csharp
using Gondwana;

Engine.Instance.UseNAudio();
```

When using `Gondwana.WinForms.Hosting`, platform configuration initializes the NAudio backend automatically. Direct WinForms users may continue to call:

```csharp
Engine.Instance.InitializeWinFormsAudioFormats();
```

which now configures this backend.

## Playback

```csharp
var sound = Engine.Managers.AudioResources.LoadFromFile("theme", "assets/theme.ogg");
sound.Volume = 0.8f;
sound.Pan = 0.0f;
sound.PlaybackSpeed = 1.25f;
sound.IsLooping = true;
sound.Play();
```

## Related packages

- `Gondwana` — core engine and backend-neutral audio contracts
- `Gondwana.Audio.Browser` — browser/WASM audio backend
- `Gondwana.Audio.Midi` — MIDI/SoundFont support built on the NAudio pipeline

## Links

- **Source:** https://github.com/Isthimius/Gondwana
- **Wiki:** https://github.com/Isthimius/Gondwana/wiki
- **API reference:** https://isthimius.github.io/Gondwana/api/
- **Release history:** https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Audio.NAudio/CHANGELOG.md
