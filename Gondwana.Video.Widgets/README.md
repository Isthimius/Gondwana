# Gondwana.Video.Widgets

Optional desktop bridge between `Gondwana.Video` and `Gondwana.Widgets`.
`VideoWidget` composes `DirectVideo` with normal Widget input, focus, lifecycle,
composite movement, opacity/fades and opt-in dragging. Neither base package depends
on the other; install this bridge only when you need interactive video.

```console
dotnet add package Gondwana.Video.Widgets
```

Also configure the desktop application's native LibVLC runtime as described in
[Gondwana.Video deployment](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Video/README.md).
The bridge does not bundle native binaries and does not support browser/WebAssembly playback.

## Normal video Widget

```csharp
using System.Drawing;
using Gondwana.Video;
using Gondwana.Video.Widgets;

var video = new VideoWidget(host, view, new Rectangle(20, 20, 640, 360),
    VideoSource.FromUri(new Uri(Path.GetFullPath("intro.mp4"))));
video.Show();
```

Use a `SceneLayer` instead of `view` with the same signature for world-space video.
View bounds are screen pixels; SceneLayer bounds are world pixels and inherit
camera, zoom and wrapping behavior. `Bounds` reads the child's actual presentation
rectangle, including letterbox space. `SetBounds(Rectangle)` moves the composite
anchor and resizes the child. `SetPosition(...)` and `Movement` move it normally.
Use these APIs for movement rather than independently changing the child's position.

The normal game hosts provide `WidgetInputRouter`; call `Show()` to register the
Widget. Custom hosts must attach/start their normal router before showing Widgets.
The Widget is pointer-enabled, focusable and keyboard-enabled. There are no
default click or keyboard playback commands.
As with other Widgets, the host must monitor the desired keys through its
keyboard poller (`StartMonitoringKey`); the wrapper does not register global keys.

## Opt-in dragging and consumer input

```csharp
video.IsDragEnabled = true; // false by default
video.DragThresholdPx = 5;
video.PointerClick += args =>
{
    // This behavior belongs to the game, not VideoWidget.
    if (args.IsPrimaryButton)
    {
        if (video.IsPlaying) video.Pause();
        else video.Play();
        args.Handled = true;
    }
};
video.KeyboardInput += args => Console.WriteLine($"Video key: {args.Key}");
video.DragEnded += args => Console.WriteLine($"Video moved to {video.Bounds.Location}");
```

Dragging anywhere within the bounds uses `DraggableWidgetBase`, including its
threshold, `IsDragging`, `DragStarted`, `Dragged`, `DragEnded`, capture and click
suppression. All ordinary Widget pointer, focus, activation and cancellation
events remain available. `Activate()` changes input priority; `SetZOrder(...)`
changes the visual child's drawing order, following existing Widget semantics.

## GAF and stream sources

```csharp
using Gondwana.Assets;

using var assets = AssetsFile.LoadOrCreate("cutscenes.gaf", null, false, register: false);
using var intro = new VideoWidget(host, view, new Rectangle(20, 20, 640, 360),
    VideoSource.FromAsset(assets, "intro")); // AssetTypes.Video
intro.Show();
// Keep these objects alive for the game screen's lifetime; dispose when leaving it.
```

`VideoSource.FromStream(stream, leaveOpen: true)` borrows a stream. The default
`leaveOpen: false` transfers ownership on successful open. Keep a borrowed stream
alive and unused until replacement/disposal; seeking and looping require a seekable
stream. GAF uses the established Video source path with a fresh owned stream per
open and no temporary extraction. Keep the AssetsFile alive if it will be reopened.

## Playback, visibility and composition

The thin playback API exposes `Play`, `Pause`, `Stop`, `Seek`, `Open`, `PlaybackRate`,
`Loop`, `Stretch`, `IsPlaying`, `Duration`, `Position`, `NaturalSize`, `HasAudio`,
`IsMetadataReady` and `Metadata`. `Video` exposes the owned `DirectVideo` for
advanced drawing configuration. Metadata snapshots remain authoritative: a false
`HasAudio` is inconclusive until metadata is ready; natural size is `(0, 0)` before
decoder discovery. Opening/replacing media starts playback, following DirectVideo.

`Show()` and `Hide()` affect visibility/input only. They never implicitly pause,
resume or restart playback. Consumers may choose that policy explicitly:

```csharp
video.Hidden += () => video.Pause();
video.Shown += () => video.Play();
video.SetOpacity(0.5f);
video.FadeIn(0.5f);
video.SetZOrder(20);
```

`Started`, `Paused`, `Stopped`, `Ended` and `StateChanged` directly forward the
player's events, preserving their sender (the player) and thread. StateChanged
includes metadata notifications. These events can run on background threads;
dispatch engine/UI changes to the engine thread. Construction may emit events
before subscribers attach, so query the current state after construction too.

## Player ownership

The default factory constructs `VlcVideoPlayer`. Supply a custom factory or instance
through the final player argument when using another backend:

```csharp
var panel = new VideoWidget(host, view, new Rectangle(20, 20, 640, 360), source,
    playerFactory: () => new VlcVideoPlayer());
// Or: new VideoWidget(host, view, bounds, source, player: myPlayer);
```

Each factory must return a fresh, unshared player. The composite owns DirectVideo;
DirectVideo alone owns/disposes that player, including an explicitly supplied
instance. Do not share or separately dispose the player/drawing while owned.
Disposing the Widget unregisters input and disposes its child once using normal
composite ownership. There is no second decoder or special disposal manager.
Constructor argument validation occurs before taking player ownership; once
DirectVideo begins opening, it cleans up its player if opening fails.

Construct, control, resize and dispose on the engine/UI thread. The existing
DirectVideo frame mailbox remains responsible for decoder/render synchronization.

See the [VideoTest demo](https://github.com/Isthimius/Gondwana/tree/master/Demos/VideoTest)
for external Widget controls, dragging and manual smoke checks, and the
[package changelog](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Video.Widgets/CHANGELOG.md).
