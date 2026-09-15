# Audio

Gondwana's audio system provides a common engine-facing API while allowing platform-specific playback backends to supply the actual audio implementation.

The core `Gondwana` package owns the public `Gondwana.Audio` contracts and resource model. Audio playback itself is provided by an optional backend package:

- [[NAudio|Audio---NAudio]] — Windows desktop playback through `Gondwana.Audio.NAudio`
- [[Browser Audio|Audio---Browser-Audio]] — browser/WASM playback through `Gondwana.Audio.Browser`
- `Gondwana.Audio.Midi` — optional MIDI/SoundFont support layered on the NAudio backend

This keeps the core engine platform-neutral and prevents desktop-specific audio dependencies from being pulled into browser or non-Windows applications.

---

## Core audio model

The common API centers on two existing engine-facing types:

- `AudioResourceManager` — loads, tracks, clones, retrieves, and unloads audio resources
- `AudioResource` — controls one loaded audio resource

Backend implementations plug into that model through:

- `IAudioBackend` — creates backend-specific playback handles
- `IAudioPlaybackHandle` — implements playback operations for one resource
- `AudioPlaybackState` — backend-neutral `Stopped`, `Playing`, and `Paused` state

Game code normally works with `AudioResourceManager` and `AudioResource`, not the backend interfaces directly.

```csharp
var music = Engine.Managers.AudioResources.LoadFromFile(
    "theme",
    "assets/theme.ogg");

music.Volume = 0.8f;
music.Pan = 0.0f;
music.PlaybackSpeed = 1.0f;
music.IsLooping = true;
music.Play();
```

The same `AudioResource` controls are used by every backend that implements them.

---

## Configuring a backend

An audio backend must be configured before loading audio.

### Windows / NAudio

```csharp
Engine.Instance.UseNAudio();
```

WinForms hosting configures the NAudio backend through its platform setup. Existing direct WinForms code may continue to use:

```csharp
Engine.Instance.InitializeWinFormsAudioFormats();
```

### Browser / WASM

After importing the browser audio JavaScript module:

```csharp
Engine.Instance.UseBrowserAudio();
```

Browser audio is then available through the normal manager:

```csharp
var music = Engine.Managers.AudioResources.LoadFromUri(
    "theme",
    "assets/theme.mp3");
```

`BrowserAudioManager` and `BrowserAudioPlayer` remain available as compatibility façades, but new code can use the common `AudioResourceManager` API.

---

## Playback controls

`AudioResource` provides the common playback surface:

| Member | Purpose |
|---|---|
| `Play(bool fromStart = true)` | Start playback, optionally from the beginning |
| `Pause()` | Pause without changing the current position |
| `Resume()` | Resume paused playback |
| `Stop()` | Stop playback |
| `Seek(TimeSpan)` | Move to a playback position |
| `CurrentTime` | Current playback position |
| `Duration` | Total duration |
| `State` | `Stopped`, `Playing`, or `Paused` |
| `IsPlaying` / `IsPaused` | Convenience state checks |
| `IsLooping` | Repeat continuously when enabled |
| `Volume` | Output volume from `0.0` through `1.0` |
| `Pan` | Stereo pan from `-1.0` through `1.0` |
| `PlaybackSpeed` | Playback rate from `0.25x` through `4.0x` |

Values outside the documented ranges are clamped.

`NaN` is rejected. Configure/load/control audio on the application's appropriate
thread (the browser thread for WASM). NAudio's position reflects decoded source
progress and may lead audible output by the device's queued buffers.

Existing NAudio-specific code should replace `PlatformAudioFactory` registration
with `NAudioReaderRegistry` in `Gondwana.Audio.NAudio`. Playback state comparisons
now use core `AudioPlaybackState`, rather than NAudio's `PlaybackState`.

The original CLR overloads for `LoadFromFile`, `LoadFromStream`,
`LoadFromEngineAssetsFile`, and `Clone` remain available. This preserves those
compiled method calls, but does not restore the removed NAudio-specific core
types or eliminate the need to configure a backend.

### Playback speed

`PlaybackSpeed = 1.0f` is normal speed.

```csharp
sound.PlaybackSpeed = 0.5f; // half speed
sound.PlaybackSpeed = 1.5f; // 50% faster
sound.PlaybackSpeed = 2.0f; // double speed
```

The common contract changes playback rate; it does not promise pitch preservation. Backend implementations may therefore change pitch naturally when playback speed changes.

---

## Loading audio

The source forms supported by a backend can differ even though playback controls are shared.

### Files and streams

Desktop backends such as NAudio support file and stream loading:

```csharp
var sound = Engine.Managers.AudioResources.LoadFromFile(
    "laser",
    "assets/laser.wav");
```

```csharp
using var stream = File.OpenRead("assets/theme.mp3");
var music = Engine.Managers.AudioResources.LoadFromStream(
    "theme",
    stream,
    ".mp3");
```

### Engine asset files

Audio entries in an `AssetsFile` can be loaded through `LoadFromEngineAssetsFile` when the configured backend supports byte/stream sources.

### URIs

URI-capable backends such as Browser Audio use:

```csharp
var music = Engine.Managers.AudioResources.LoadFromUri(
    "theme",
    "assets/theme.mp3");
```

Browser-relative paths are resolved by the browser application.

---

## Resource management

Loaded resources are keyed by name.

```csharp
var audio = Engine.Managers.AudioResources;

if (audio.TryGet("theme", out var theme))
    theme.Play();

audio.Unload("theme");
audio.Clear();
```

`Clone` creates another independently controlled resource when the original source can be recreated by the current backend.

```csharp
var secondLaser = audio.Clone("laser", "laser-2", volume: 0.5f);
```

Serialized engine state retains the source metadata and portable playback settings. Restoring an audio resource requires the appropriate backend to have been configured first.

---

## Completion and disposal

`AudioResource.PlaybackCompleted` is raised by backends that can report non-looping completion to .NET. `PlaybackCompletedAsync` provides the corresponding asynchronous callback hook.

```csharp
sound.PlaybackCompleted += (_, _) =>
{
    // advance music, dispose an effect, etc.
};
```

Both supplied backends raise completion for natural, non-looping playback. Browser Audio forwards the DOM `ended` event into .NET and changes state to `Stopped`. Pause, explicit stop, and unload do not raise completion.

`AudioResource.Dispose()` releases the backend handle. `AudioResourceManager.Unload` and `Clear` dispose resources automatically.

---

## Backend selection

The core package intentionally does not silently choose an audio backend. Loading audio without one configured throws an actionable exception.

This keeps platform selection explicit and prevents the core engine from acquiring a hidden dependency on a specific desktop or browser implementation.

See also:

- [[NAudio|Audio---NAudio]]
- [[Browser Audio|Audio---Browser-Audio]]
- [[Assets Files]]
- [[Serialization and EngineState]]
