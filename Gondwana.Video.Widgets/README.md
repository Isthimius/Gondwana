# Gondwana.Video.Widgets

Optional desktop bridge between `Gondwana.Video` and `Gondwana.Widgets`.
`VideoWidget` composes `DirectVideo` with normal Widget input, focus, lifecycle,
composite movement, opacity/fades and opt-in dragging. Neither base package depends
on the other; install this bridge only when you need interactive video.

```csharp
using Gondwana.Video;
using Gondwana.Video.Widgets;

var video = new VideoWidget(host, view, new Rectangle(20, 20, 640, 360),
    VideoSource.FromUri(new Uri(Path.GetFullPath("intro.mp4"))));
video.Show();
```

Construction starts playback, following DirectVideo. Show/Hide do not change playback.
Dragging defaults to false. The widget owns its DirectVideo and supplied or
factory-created player; dispose the widget once when done. Native LibVLC runtime
and platform requirements remain those in [Gondwana.Video](../Gondwana.Video/README.md).
