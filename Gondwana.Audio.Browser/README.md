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

Browser assets are loaded by URI. The current backend does not implement the byte/stream loading path used by `LoadFromFile`, `LoadFromStream`, or packed `AssetsFile` audio.

Autoplay remains subject to browser policy; games should normally begin playback in response to user interaction when the browser blocks autoplay.

The JavaScript bridge tracks the HTML media `ended` event and changes playback state to `Stopped`, but the current bridge does not yet marshal that DOM event back into .NET. `AudioResource.PlaybackCompleted` and `PlaybackCompletedAsync` are therefore not currently raised by Browser Audio.

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
