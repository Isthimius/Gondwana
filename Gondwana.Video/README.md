# Gondwana.Video

Optional **native desktop** video playback for .NET 8: `DirectVideo` → `IVideoPlayer` →
`VlcVideoPlayer` → LibVLCSharp / LibVLC 3. Core Gondwana has no dependency on Video.
Blazor/browser playback is not supported by this backend.

## Install and deploy

```console
dotnet add package Gondwana.Video
```

This package includes the managed binding, **not a self-contained native runtime**.
Add native dependencies to the executable application, not to the cross-platform
Gondwana.Video package. This follows [VideoLAN's deployment model](https://github.com/videolan/libvlcsharp/tree/3.x#installation).

| Desktop target | Native runtime |
| --- | --- |
| Windows | `dotnet add package VideoLAN.LibVLC.Windows --version 3.0.23.1`. Restore/build/publish copies LibVLC and its plugins beside the app. No machine-wide VLC install is required. |
| macOS | Add `VideoLAN.LibVLC.Mac` (currently 3.1.3.1) to an application targeting a compatible architecture. Check the package's native architectures before choosing x64/arm64. For an architecture not supplied by the package, deploy a matching LibVLC 3 build and initialize its directory explicitly. Signing/notarization remains the application's responsibility. |
| Linux | Install distribution runtime and codec plugins, e.g. Debian/Ubuntu: `sudo apt install libvlc-dev vlc-plugin-base`. Include the matching runtime/plugins in your deployment image. Other distributions use their equivalent packages; there is no official Linux native NuGet in VideoLAN's deployment table. |

Match the native architecture to the **running process**, including x64 vs arm64.
Keep the native plugins directory in the package's output layout; copying only
`libvlc.dll`/`libvlc.so`/`libvlc.dylib` is insufficient. Do not assume .NET single-file,
trimming, or NativeAOT publishing bundles or preserves the runtime: these deployment
modes are not validated here. Validate the final published application on each OS.

For custom runtime placement, call `LibVLCSharp.Shared.Core.Initialize(nativeDirectory)`
once before creating a player. See [VideoLAN initialization guidance](https://docs.videolan.me/libvlcsharp/docs/getting_started.html)
and the [Linux setup guide](https://docs.videolan.me/libvlcsharp/docs/linux-setup.html).
Missing/incompatible runtime loading is wrapped in an actionable `InvalidOperationException`
with the original exception preserved. Playback/codec failures are reported through
`StateChanged` (`Error`) and `VlcVideoPlayer.LastError`.

`gondwana doctor` explains app-local deployment when the current project directly
references Video. Its own process cannot verify another application's native output;
a system-runtime probe is not proof that a published game is complete. Development
setup no longer silently installs global VLC for this optional component.

## URI playback

Create and control drawings on the engine/UI thread. The drawing owns its player,
opens the source, and starts playback. Bounds are required `Rectangle` values in
both constructor modes; source dimensions are discovered later.

```csharp
using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Video;

var player = new VlcVideoPlayer();
var video = new DirectVideo(player,
    new Uri(Path.GetFullPath("assets/video/intro.mp4")),
    renderSurfaceHost, view, new Rectangle(50, 50, 800, 450), "IntroVideo")
{
    Stretch = StretchMode.Uniform,
    Loop = false,
    Opacity = 1f
};

video.Pause();
video.Play();
video.Seek(TimeSpan.FromSeconds(10));
video.PlaybackRate = 0.5;
video.FadeOut(1); // inherited DirectDrawingBase composition, applied exactly once
// Dispose the drawing when its scene/UI is finished with it.
```

For world-space playback pass a `SceneLayer` instead of a `View` and world-pixel
bounds. Both modes use the normal CPU/GPU drawing paths. `Uniform` letterboxes;
`UniformToFill` preserves aspect and clips overflow to the presentation bounds.
`None` uses decoded pixel dimensions; `Fill` ignores aspect ratio.

Use `video.Open(newUri)` or `video.Open(videoSource)` on the engine thread to replace
media; queued/visible frames from the old source are cleared. Direct player reopening
also resets the mailbox; its visual change is consumed at the next engine update.

## Metadata and dimensions

`Open(Uri)` remains synchronous and returns after starting asynchronous parsing;
it does not wait for network metadata. Read `player.Metadata` for one immutable
snapshot with `Unavailable`, `Pending`, `Ready`, or `Failed` status. `HasAudio=false`
and zero duration are **unknown**, not authoritative, unless status is `Ready`.
Even ready live media can have an unknown (zero) duration.

`IsMetadataReady` is a convenience check. `StateChanged` reports `MetadataReady` or
`MetadataFailed`. Poll the snapshot during your engine update, or marshal events to
that thread before changing engine objects. Player lifecycle/metadata events can
arrive on worker threads. Never block them waiting for the engine/UI thread.
Reopening cancels the old parser, removes its handlers, and ignores obsolete results.

Stream media is not preparsed concurrently against its playback cursor. Its metadata
becomes ready when playback has discovered tracks. Non-seekable streams can play,
but seeking, restarting and looping depend on their backend/format capabilities.

`NaturalSize` is `(0,0)` until LibVLC negotiates decoded dimensions, then reflects
that format (including subsequent resolution changes). `initialWidth`/`initialHeight`
remain accepted for source compatibility as legacy hints; neither forces a decode
size, allocates a nominal 1280×720 frame, nor substitutes for actual dimensions.
Gondwana/Skia handles presentation scaling. Non-square-pixel media and unusual
filter/rotation configurations should be checked in the smoke demo.

## GAF and streams

```csharp
using Gondwana.Assets;
using Gondwana.Drawing.Direct;
using Gondwana.Video;

using var assets = AssetsFile.LoadOrCreate("assets/game.gaf");
var video = new DirectVideo(() => new VlcVideoPlayer(),
    VideoSource.FromAsset(assets, "intro.mp4"),
    renderSurfaceHost, view, new Rectangle(0, 0, 960, 540))
{
    Stretch = StretchMode.Uniform
};
```

`FromAsset` selects `AssetTypes.Video` using normal GAF name resolution and obtains
a fresh owned stream for each Open. No extraction file is created. The archive must
be alive when the source is opened. Current GAF streams are independent memory
streams, so playback can continue after the archive closes. GAF currently loads
asset bytes in memory; this bridge does not make large archives disk-streaming.

For a caller-provided stream use `VideoSource.FromStream(stream, leaveOpen: true)`
with `DirectVideo`, or `player.Open(stream, leaveOpen: true)` directly. Default
`leaveOpen: false` transfers ownership **on successful Open**: reopening or disposal
closes it. On failure the caller still owns its stream. Borrowed streams must remain
open and exclusively available to the player until replacement/disposal. Do not
reuse an already-owned stream source after it has been closed. The VLC media-input
object stays alive for the entire active media lifetime.

Existing backend implementations remain source-compatible through default interface
members: stream Open throws `NotSupportedException` unless implemented, and metadata
is unavailable unless implemented. The corrected frame contract is BGRX; custom
backends previously supplying RGBA must update their output.

## Frame contract and threading

LibVLC 3's [`vmem` implementation](https://github.com/videolan/vlc/blob/3.0.x/modules/video_output/vmem.c)
uses masks `R=0xff0000`, `G=0xff00`, `B=0xff` for RV32. On supported little-endian
desktops its bytes are **B, G, R, X**; X is not alpha. Big-endian and browser targets
are rejected. The render bitmap uses `SKColorType.Bgra8888` and opaque alpha, with no
red/blue swap. Copying normalizes X to 255 because opaque Skia raster copies may
still preserve the unused byte. Transparency comes from inherited drawing opacity.

`FrameReady`'s pointer is valid only during that callback. Subscribers must copy
before returning and must not call player controls or engine/render operations.
`DirectVideo` copies into one reusable latest-frame mailbox. `Update` consumes it,
updates the render-owned bitmap, and calls `ForceRefresh`. Intermediate frames are
dropped. Storage is bounded; steady-state frames do not allocate large managed
arrays. Native output buffers are aligned and recreated when LibVLC renegotiates.
`Dispose` closes the mailbox before stopping/joining native callbacks.

## Reproducible desktop smoke test

For a clean Windows consumer using a release that contains these APIs:

```console
dotnet new install Gondwana.Templates
dotnet new gondwana-winforms -n VideoConsumer
cd VideoConsumer
dotnet add package Gondwana.Video
dotnet add package VideoLAN.LibVLC.Windows --version 3.0.23.1
```

Keep the template's default GPU host. Configure a full view in `CreateInitialViews`
with `RenderSurface.Host.ViewManager.ConfigureSingleFullView()`. In
`CreateDirectDrawings`, use the URI example with
`renderSurfaceHost = RenderSurface.Host` and `view = renderSurfaceHost.ViewManager.Views[0]`.
Supply your own absolute media path. Run `dotnet run -c Release`; then
`dotnet publish -c Release -r win-x64 --self-contained false` and run the published
executable on a Windows machine with .NET 8 but **without VLC installed**. The
native NuGet supplies LibVLC; .NET itself is separate.

For this branch, the ready-to-run template-based GPU demo is:

```console
dotnet run --project Demos/VideoTest -c Release -- "C:\media\clip.mp4"
dotnet run --project Demos/VideoTest -c Release -- "C:\media\clip.mp4" --stream
dotnet run --project Demos/VideoTest -c Release -- "C:\media\game.gaf" --gaf intro.mp4
dotnet run --project Demos/VideoTest -c Release -- "C:\media\clip.mp4" --headless
```

The headless check disables audio output but verifies audio **track detection**,
URI/stream/GAF decoding, controls, actual loop restart frames, Stop from an Ended
handler, repeated opening and disposal. It also generates temporary lossless red/blue
images to check native BGRX channel order and exact 640×480 / 1920×1080 dimensions.
It requires native LibVLC but no GPU/window; it is opt-in and
excluded from normal CI. The window uses the normal GPU host and audible playback.

Use a known-color clip with a recognizable circle/square, audio, and at least a few
seconds of duration; repeat with 640×480 and 1920×1080 sources. Do not commit media.
If FFmpeg is installed, generate a local fixture:

```console
ffmpeg -f lavfi -i testsrc2=size=640x480:rate=30 -f lavfi -i sine=frequency=440:sample_rate=48000 -t 6 -c:v mpeg4 -q:v 2 -c:a aac video-smoke.mp4
```

Check visible frames, red/blue colors, aspect ratio and the title's actual natural
size and audio status. Exercise Pause/Play, Seek +5s, Loop through the end, 0.5x/1x/2x,
Stop/Play, Fade out/in, Dispose, and close the window during playback. Repeat the
stream and GAF paths. Confirm borrowed streams stay open and owned streams close.
Resolution renegotiation is unit-tested without a GPU; also test an adaptive or
concatenated source on each supported native runtime if your application uses it.

## Validation and scope

Normal `Gondwana.Tests` uses fake players and synthetic pixels: no LibVLC install,
network, sound device or display is required. It covers pixel/alpha/stride copying,
concurrency, frame replacement, dimensions, opacity/fades, stretch, disposal,
metadata generations, source replacement and GAF/stream ownership. Native smoke
results are platform-specific; macOS/Linux playback and visual/audio checks still
need to be run on their target machines.

License: MIT for Gondwana. Native LibVLC and its plugins retain their own licenses.
