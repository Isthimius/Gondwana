# Gondwana.Audio.Browser

**Gondwana.Audio.Browser** adds HTML5 Audio API playback support to the Gondwana Game Engine for browser/WASM targets built with `net8.0-browser`.

It uses .NET 8's native JavaScript interop (`[JSImport]`) to route all audio operations through a lightweight JavaScript ES module, bypassing the desktop NAudio pipeline that is unavailable in WebAssembly.

## Features

- Plays `.mp3`, `.wav`, `.ogg`, and any other format supported by the host browser
- Volume and loop control
- Play, pause, stop, and seek
- Zero external .NET dependencies — only `System.Runtime.InteropServices.JavaScript`
- Ships the `gondwana-audio.js` module as a NuGet content file (automatically placed in `wwwroot/`)

## Installation

```bash
dotnet add package Gondwana.Audio.Browser
```

The NuGet package automatically copies `gondwana-audio.js` into your project's `wwwroot/` folder so that it is included in the WASM `AppBundle` on publish.

## Setup

### 1. Import the JS module before Avalonia starts

In `Program.Browser.cs`:

```csharp
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

[SupportedOSPlatform("browser")]
private static async Task Main(string[] args)
{
    // Import the gondwana-audio JS module before starting Avalonia.
    await JSHost.ImportAsync("gondwana-audio", "./gondwana-audio.js");
    await BuildAvaloniaApp().StartBrowserAppAsync("out");
}
```

### 2. Use `BrowserAudioManager` in your game host

```csharp
protected override void LoadAssets()
{
    if (OperatingSystem.IsBrowser())
    {
        var audio = Engine.GetBrowserAudioManager();

        _music = audio.Load("music", "assets/theme.mp3", volume: 0.5f, loop: true);
        _sfx   = audio.Load("click", "assets/click.wav");
    }
    else
    {
        // Desktop: use the NAudio-based AudioResourceManager
        _desktopMusic = Engine.Managers.AudioResources.LoadFromFile("music", @"assets\theme.mp3");
        _desktopMusic.IsLooping = true;
    }
}

protected override void OnStartEngine()
{
    if (OperatingSystem.IsBrowser())
        _music?.Play();
    else
        _desktopMusic?.Play();
}
```

## API

### `BrowserAudioManager`

| Method | Description |
|---|---|
| `Load(key, src, volume, loop)` | Load a track; returns a `BrowserAudioPlayer`. |
| `Unload(key)` | Stop and release a track. |
| `UnloadAll()` | Stop and release all tracks. |
| `TryGet(key, out player)` | Try to retrieve a loaded player. |
| `Get(key)` | Get a player or `null`. |
| `Contains(key)` | Check whether a key is loaded. |

### `BrowserAudioPlayer`

| Member | Description |
|---|---|
| `Key` | The unique track identifier. |
| `Volume` | Volume in [0.0, 1.0]. |
| `IsLooping` | Whether the track loops. |
| `Play(fromStart)` | Start or resume playback. |
| `Pause()` | Pause without resetting. |
| `Stop()` | Stop and seek to beginning. |

## Notes

- Audio autoplay is subject to browser policy: the first `Play()` call should be triggered by a user gesture.
- The `gondwana-audio.js` file must be reachable at `./gondwana-audio.js` relative to `index.html` in the `AppBundle`.

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Audio.Browser/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine
-   `Gondwana.Blazor` --- Web assembly rendering and input adapters
-   `Gondwana.Blazor.Hosting` --- Blazor-specific game host that integrates rendering and input into the Gondwana lifecycle
-   `Gondwana.Hosting` --- Standard platform-agnostic scaffolding for initializing and running Gondwana games
-   `Gondwana.Widgets` --- UI widget library for creating in-game menus, HUDs, and overlays

## License

MIT
