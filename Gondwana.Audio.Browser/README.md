# Gondwana.Audio.Browser

**Gondwana.Audio.Browser** provides the browser/WASM implementation of Gondwana's backend-neutral `Gondwana.Audio` contracts.

The package uses .NET JavaScript interop plus HTML media and Web Audio APIs. New code can use the same `Engine.Managers.AudioResources` / `AudioResource` surface used by desktop audio.

## Features

- Browser-supported media formats such as MP3, WAV, and OGG
- play, pause, resume, stop, seek, and looping
- volume control
- stereo pan through the Web Audio API when `StereoPannerNode` is available
- playback speed from 0.25x through 4.0x
- current position, duration, and common playback state
- URI-based loading through `AudioResourceManager.LoadFromUri`
- compatibility `BrowserAudioManager` / `BrowserAudioPlayer` wrappers for existing code
- ships `gondwana-audio.js` as NuGet content

## Installation

```bash
dotnet add package Gondwana.Audio.Browser
```

## Setup

Import the JavaScript module before using browser audio:

```csharp
await JSHost.ImportAsync("gondwana-audio", "./gondwana-audio.js");
```

Then configure the backend:

```csharp
Engine.Instance.UseBrowserAudio();
```

Load and control audio through the common API:

```csharp
var music = Engine.Managers.AudioResources.LoadFromUri(
    "music",
    "assets/theme.mp3",
    volume: 0.5f);

music.IsLooping = true;
music.Pan = 0.0f;
music.PlaybackSpeed = 1.0f;
music.Play();
```

The Gondwana Blazor template imports the JavaScript module for you.

## Compatibility API

Existing code can continue to use:

```csharp
var audio = Engine.Instance.GetBrowserAudioManager();
var music = audio.Load("music", "assets/theme.mp3", volume: 0.5f, loop: true);
```

`BrowserAudioManager` and `BrowserAudioPlayer` now act as compatibility façades over the common core audio model.

## Browser-specific behavior

Browser audio supports both URI-addressable assets and raw byte/stream sources. Byte-backed resources, including audio entries from a streamed or file-backed `AssetsFile`, are exposed to the browser through temporary Blob URLs that are revoked when the resource is unloaded.

Autoplay remains subject to browser policy; games should normally begin playback in response to user interaction when the browser blocks autoplay.

The JavaScript bridge forwards the HTML media `ended` event into .NET, changes playback state to `Stopped`, and raises `AudioResource.PlaybackCompleted` and `PlaybackCompletedAsync` for non-looping playback. Explicit stop and unload do not raise completion.

Pan uses Web Audio's `StereoPannerNode` when available. Cross-origin media must permit anonymous CORS access. Duration is zero until browser metadata is available; seeking and codec support follow browser capabilities.

All tracks share one lazily created `AudioContext` for the module's lifetime.
Unloading a track disconnects its source/panner nodes without closing the shared
context or interrupting other tracks. The context is retained until page teardown.

The original four-argument `BrowserAudioManager.Load` CLR overload is retained for
compiled clients. New source callers can also supply pan and playback speed.

Actual codec support is determined by the host browser and operating system.

## Related packages

- `Gondwana` — core engine and backend-neutral audio contracts
- `Gondwana.Audio.NAudio` — Windows desktop audio backend
- `Gondwana.Blazor` — browser rendering and input support
- `Gondwana.Blazor.Hosting` — browser game hosting

## Documentation

- **Wiki:** https://github.com/Isthimius/Gondwana/wiki/Audio
- **API reference:** https://isthimius.github.io/Gondwana/api/
- **Release history:** https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Audio.Browser/CHANGELOG.md

## License

MIT
