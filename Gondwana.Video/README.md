# Gondwana.Video

**Gondwana.Video** adds video playback capabilities to the Gondwana Game Engine using LibVLCSharp.

It supports embedding video playback into scenes for cutscenes, backgrounds, or UI elements.

## Features

- Video playback support
- Integration with Gondwana rendering
- Backed by LibVLCSharp
- Suitable for cutscenes and overlays

## Installation

```bash
dotnet add package Gondwana.Video
```

## Requirements

- VLC runtime libraries must be available on the target system

## Usage

Initialize video support through the rendering or media system:

```csharp
// Assume this is inside your GameHost or initialization flow

// 1. Engine pieces
var renderSurfaceHost = host.Engine.RenderSurfaceHost;
var view = host.Engine.Managers.Views.PrimaryView;

// 2. Create the VLC-backed video player
IVideoPlayer player = new VlcVideoPlayer(
    vlcArgs: new[]
    {
        "--no-audio-time-stretch",
        "--no-snapshot-preview"
    },
    initialWidth: 1280,
    initialHeight: 720);

// Optional: configure behavior
player.Loop = true;

// 3. Define source
var source = new Uri("assets/video/intro.mp4", UriKind.Relative);

// 4. Define screen-space bounds (HUD-style)
var bounds = new Rectangle(50, 50, 800, 450);

// 5. Create DirectVideo (this will call Open + Play internally in your pipeline)
var video = new DirectVideo(
    player,
    source,
    renderSurfaceHost,
    view,
    bounds,
    name: "IntroVideo");
```

## Documentation

-   **[Source Code](https://github.com/isthimius/Gondwana)**
-   **[Architecture & Guides](https://github.com/isthimius/Gondwana/wiki)**
-   **[API Reference (Doxygen)](https://isthimius.github.io/Gondwana/)**
-   **[Release History](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Video/CHANGELOG.md)**

## Related Packages

-   `Gondwana` --- Core engine
-   `Gondwana.WinForms` --- WinForms rendering and input adapters
-   `Gondwana.WinForms.Hosting` --- WinForms-specific game host that integrates rendering and input into the Gondwana lifecycle

## License

MIT
