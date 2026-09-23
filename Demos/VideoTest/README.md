# VideoTest

Opt-in Windows GPU video dogfooding demo based on the current New Project template.
Native LibVLC is supplied by the official VideoLAN.LibVLC.Windows NuGet dependency.
No media is included. See [Gondwana.Video smoke instructions](../../Gondwana.Video/README.md#reproducible-desktop-smoke-test)
for URI, stream, GAF, headless and published-consumer commands and the manual checklist.

The headless check does not validate audible output or GPU presentation. Native
log messages during demux probing/stop can be diagnostic noise; the check fails on
missing metadata/frames, loop timeout, ownership errors or a reported playback failure.

## Interactive VideoWidget checks

```console
dotnet run --project Demos/VideoTest -c Release -- "C:\media\clip.mp4"
dotnet run --project Demos/VideoTest -c Release -- "C:\media\clip.mp4" --stream
dotnet run --project Demos/VideoTest -c Release -- "C:\media\cutscenes.gaf" --gaf intro
```

The GPU demo now composes `VideoWidget` with **separate ordinary ButtonWidgets**.
The core Widget contains no buttons or playback chrome. `IsDragEnabled = true`
is an explicit demo choice; package defaults remain false. The game host attaches
the standard Widget input router.

- Drag the video anywhere; its pixels and hit area must move together. Reset bounds restores it.
- Hover/click changes the status label. A completed drag must not increment the click counter.
- Drag toggle disables movement. A–Z, Space, Enter and Escape after focusing video log the key without playback shortcuts; the demo host monitors these keys.
- Play/Pause/Stop, seek +5 seconds, rate and loop buttons use the Widget's thin playback API.
- Hide must remove pixels/input while playback time continues; Show must not restart a paused video.
- Fade in/out and 50% opacity should affect the video once. Show/Hide retains normal opacity semantics.
- Stretch next cycles None/Fill/Uniform/UniformToFill; Resize changes presentation bounds.
- Controls remain in front of dragged video using normal drawing/input order.
- Dispose removes the video and releases its player/stream. Close the window during playback or dragging too.

The title shows metadata, natural dimensions, position, looping, stretch and drag
state. Supply your own local media; no video binaries are committed. The unchanged
`--headless` mode continues to check the native backend independently of Widgets.
