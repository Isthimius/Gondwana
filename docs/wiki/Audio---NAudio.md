# NAudio

`Gondwana.Audio.NAudio` is Gondwana's Windows desktop audio backend. It implements the backend-neutral contracts defined by `Gondwana.Audio` in the core `Gondwana` package.

NAudio is therefore an optional platform implementation rather than a dependency of the engine core.

---

## Package

```sh
dotnet add package Gondwana.Audio.NAudio
```

The package targets `net8.0-windows` and currently uses:

- NAudio 2.3.0
- NAudio.Vorbis 1.5.0

Configure it directly with:

```csharp
Engine.Instance.UseNAudio();
```

WinForms hosting configures the backend as part of platform initialization. Existing direct WinForms applications may continue to call:

```csharp
Engine.Instance.InitializeWinFormsAudioFormats();
```

---

## Supported sources

The NAudio backend supports Gondwana's byte/stream-based loading paths:

- loose files through `LoadFromFile`
- streams through `LoadFromStream`
- audio entries from `AssetsFile`
- cloning when the source bytes are retained

`LoadFromUri` is not implemented by this backend. URI-addressable browser content belongs to [[Browser Audio|Audio---Browser-Audio]].

---

## Default formats

`NAudioReaderRegistry` provides the default decoder mapping:

| Extension | Reader |
|---|---|
| `.wav` | `WaveFileReader` |
| `.mp3` | `Mp3FileReader` |
| `.ogg`, `.oga`, `.mogg` | `VorbisWaveReader` |
| `.wma`, `.m4a` | `MediaFoundationReader` |

WMA and M4A require a temporary file because Media Foundation readers are path-based. The backend creates and removes that temporary file automatically.

Additional NAudio-compatible formats can be registered by extension through `NAudioReaderRegistry.Register` or `RegisterFile`.

---

## Playback graph

The NAudio backend builds its audio graph inside `Gondwana.Audio.NAudio`; none of these NAudio-specific types are exposed by core Gondwana.

Conceptually:

```text
WaveStream
    |
    v
ISampleProvider
    |
    v
VariableSpeedSampleProvider
    |
    v
Pan / stereo balance
    |
    v
VolumeSampleProvider
    |
    v
WaveOutEvent
```

Mono sources use NAudio's `PanningSampleProvider`. Stereo sources use Gondwana's NAudio-specific stereo pan provider. Sources with more than two channels are reduced to their first two channels before stereo pan is applied.

---

## Playback speed

The common `AudioResource.PlaybackSpeed` property is implemented by a variable-rate sample provider in this package.

```csharp
sound.PlaybackSpeed = 1.25f;
```

Supported values are `0.25f` through `4.0f`; values outside that range are clamped by the common audio API.

The provider advances through source frames at the requested rate and interpolates between adjacent frames. Playback speed therefore changes pitch naturally. Pitch-preserving time stretching is not part of the Gondwana audio contract at this time.

Playback speed can be changed while the resource is playing.

---

## Seeking and looping

Seeking updates the underlying `WaveStream` position and resets the variable-speed interpolation state before playback continues.

Looping remains a Gondwana-level playback behavior. When a non-requested NAudio stop reaches the end of the stream and `IsLooping` is enabled, playback restarts from the beginning.

For non-looping audio, the NAudio backend forwards completion to `AudioResource.PlaybackCompleted` and `PlaybackCompletedAsync`.

---

## MIDI

`Gondwana.Audio.Midi` is an optional extension of the NAudio pipeline.

It uses MeltySynth to synthesize `.mid` and `.midi` data into an NAudio `WaveStream`, then registers those extensions with `NAudioReaderRegistry`.

```csharp
Engine.Instance.UseNAudio();
Engine.Instance.InitializeMidiAudioFormats();
```

`Gondwana.Audio.Midi` therefore depends on `Gondwana.Audio.NAudio`; MIDI implementation details do not belong in the core audio contracts.

---

## Platform scope

The current package is intentionally Windows-specific because the NAudio version used by Gondwana 2.6.0 is the existing Windows-oriented implementation.

If Gondwana later adopts a newer cross-platform NAudio stack, that change can remain inside `Gondwana.Audio.NAudio` without changing the core `Gondwana.Audio` API.

See also:

- [[Audio]]
- [[Browser Audio|Audio---Browser-Audio]]
