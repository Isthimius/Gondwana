# Browser Audio

`Gondwana.Audio.Browser` provides audio playback for browser/WASM targets. It implements the common `Gondwana.Audio` contracts using HTML media and the Web Audio API through JavaScript interop.

New browser code can therefore use the same `AudioResourceManager` and `AudioResource` surface as desktop code.

---

## Package

```sh
dotnet add package Gondwana.Audio.Browser
```

The package targets `net8.0-browser` and includes `gondwana-audio.js` as NuGet content.

Before using browser audio, import that module during browser startup:

```csharp
await JSHost.ImportAsync("gondwana-audio", "./gondwana-audio.js");
```

Then configure the backend:

```csharp
Engine.Instance.UseBrowserAudio();
```

The generated Gondwana Blazor template imports the browser audio module for you.

---

## Loading audio

Browser audio is URI-based:

```csharp
var music = Engine.Managers.AudioResources.LoadFromUri(
    "theme",
    "assets/theme.mp3");
```

The source may be a browser-relative or absolute URL that the browser can load.

Pan uses Web Audio and cross-origin media must allow anonymous CORS requests.
Duration is zero until browser metadata is available. URI loading, autoplay,
codec support, and seekability remain browser/platform constraints.

The browser backend supports both URI and byte/stream loading. Byte-backed audio, including entries read from a streamed or file-backed `AssetsFile`, is exposed to `HTMLAudioElement` through a temporary Blob URL and can therefore use the same `AudioResourceManager.LoadFromStream` and packed-asset paths as desktop backends.

---

## Common playback API

A browser-backed `AudioResource` supports the common controls:

```csharp
music.Volume = 0.75f;
music.Pan = -0.25f;
music.PlaybackSpeed = 1.25f;
music.IsLooping = true;

music.Play();
music.Pause();
music.Resume();
music.Seek(TimeSpan.FromSeconds(10));
music.Stop();
```

The backend also reports:

- `State`
- `IsPlaying`
- `IsPaused`
- `CurrentTime`
- `Duration`

---

## Volume and looping

Volume and looping map directly to `HTMLAudioElement.volume` and `HTMLAudioElement.loop`.

Volume is clamped to the common Gondwana range `0.0` through `1.0`.

---

## Stereo pan

HTML media elements do not expose a direct pan property, so Gondwana uses the Web Audio API when available:

```text
HTMLAudioElement
      |
      v
MediaElementAudioSourceNode
      |
      v
StereoPannerNode
      |
      v
AudioContext.destination
```

`AudioResource.Pan` maps to the `StereoPannerNode` range `-1.0` through `1.0`.

Tracks share one lazily created `AudioContext` for the module's lifetime, with
separate source/panner nodes per track. Unloading disconnects only that track's
nodes; the shared context stays available until page teardown.

If the browser cannot create the Web Audio graph, ordinary media playback still works but stereo pan is unavailable for that element.

---

## Playback speed

`AudioResource.PlaybackSpeed` maps to `HTMLAudioElement.playbackRate`.

```csharp
music.PlaybackSpeed = 0.75f;
music.PlaybackSpeed = 2.0f;
```

The common Gondwana range is `0.25x` through `4.0x`.

As with the NAudio backend, Gondwana does not promise pitch preservation as part of the common playback-speed contract. Browser pitch behavior may also vary with browser implementation details.

---

## Browser autoplay rules

Modern browsers may reject `play()` until the user has interacted with the page.

Gondwana catches the rejected JavaScript play promise so the browser does not surface it as an unhandled error, but it cannot bypass browser autoplay policy. Games should normally begin music or effects in response to the first user input when autoplay is restricted.

---

## Completion behavior

The JavaScript bridge tracks the media element's `ended` event and updates the playback state to `Stopped`.

The bridge forwards the DOM event through its .NET interop callback, raising `AudioResource.PlaybackCompleted` and `PlaybackCompletedAsync` for non-looping playback. Explicit stop and unload do not raise completion; unloading removes the callback.

---

## Compatibility API

Existing browser code can continue to use:

```csharp
var manager = Engine.Instance.GetBrowserAudioManager();
var music = manager.Load("theme", "assets/theme.mp3");
```

`BrowserAudioManager` and `BrowserAudioPlayer` now act as compatibility façades over the common `AudioResourceManager` / `AudioResource` implementation.

New code should generally prefer:

```csharp
Engine.Instance.UseBrowserAudio();
var music = Engine.Managers.AudioResources.LoadFromUri("theme", "assets/theme.mp3");
```

---

## Format support

Actual media format support is controlled by the browser rather than Gondwana. MP3, WAV, OGG, and other formats depend on the browser, operating system, and codec support available to the user.

See also:

- [[Audio]]
- [[NAudio|Audio---NAudio]]
