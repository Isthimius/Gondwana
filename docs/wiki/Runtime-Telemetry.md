# Runtime telemetry

`Engine.Instance.Profiler` provides opt-in CPU measurements without SceneViewer or
Studio. It is disabled until an application, `ProfilerWidget`, or the Scene Viewer's F3 diagnostics overlay holds a collection request. That F3 binding belongs specifically to the saved-scene Scene Viewer used by the `.gscn` editor/Studio tooling (and direct Scene Viewer launches); it is not a general Engine hotkey.

The repository also includes `docs/examples/RuntimeTelemetryExample.cs`, compiled
and exercised by `Gondwana.Tests` without a viewer dependency.

```csharp
using Gondwana;
using Gondwana.Diagnostics;

var profiler = Engine.Instance.Profiler;
profiler.Configure(new TelemetryOptions
{
    Interval = TimeSpan.FromMilliseconds(250),
    HistoryCapacity = 120
}); // configure before starting collection

using var collection = profiler.Start();
// Keep collection alive while the game runs. At your own modest display cadence:
var snapshot = profiler.GetLatestSnapshot();
if (snapshot is not null)
{
    foreach (var source in snapshot.Sources)
    {
        if (source.Metrics.TryGetValue("cycle.cpu.ms", out var cycle) &&
            cycle.Availability == TelemetryAvailability.Available)
            Console.WriteLine($"CPS={cycle.Count / snapshot.ElapsedSeconds:F1}; CPU mean={cycle.Mean:F3} ms");
    }
}
var recent = profiler.GetHistory(); // non-consuming, oldest to newest
// Dispose collection when this consumer no longer needs measurements.
```

`TelemetryOptions` keeps both retained memory and publication work bounded. In
addition to the documented per-property ranges:

- `HistoryCapacity * SourceCapacity * MetricCapacity` may not exceed **1,048,576**
  retained metric-summary slots.
- `SourceCapacity * MetricCapacity / Interval.TotalSeconds` may not exceed
  **262,144 configured metric-summary materializations per second**.

The first budget prevents individually legal maxima from combining into a
multi-gigabyte retained history. The second prevents very short intervals combined
with maximum source/metric capacities from forcing millions of summary/dictionary
allocations per second while holding the producer lock. The default configuration
uses 245,760 retained slots and a worst-case publication rate of 8,192 configured
metric summaries per second.

## In-game ProfilerWidget

Games that reference `Gondwana.Widgets` can display the same detached runtime telemetry
without building their own diagnostics text:

```csharp
using System.Drawing;
using Gondwana.Widgets.Hud;

var profilerWidget = new ProfilerWidget(
    host,
    view,
    new Rectangle(12, 12, 700, 520));

profilerWidget.SetMeasurementVisible("layers.omitted", false);
profilerWidget.Show();
```

`ProfilerWidget` is a view-level HUD widget. `Show()` acquires one independent
`Engine.Instance.Profiler.Start()` request, while `Hide()` and `Dispose()` release
that request. Merely constructing the widget does not keep telemetry collection active.

By default it displays all sampled measurements from all visible sources and omits
`Unsupported`, `NotApplicable`, and `NotYetSampled` rows. The label is vertically
scrollable when the selected measurements exceed its bounds. Display refresh defaults
to 250 ms and is independent of the profiler's aggregation interval.

Individual measurements can be hidden or shown globally:

```csharp
profilerWidget.SetMeasurementVisible("snapshot.age.ms", false);
profilerWidget.SetMeasurementVisible("cycle.cpu.ms", true);
```

Or for one named source:

```csharp
profilerWidget.SetMeasurementVisible(
    "Render surface",
    "snapshot.commands.approximate",
    false);
```

For a compact allow-list, switch to selected mode and explicitly enable only the
measurements needed for the current investigation:

```csharp
profilerWidget.MeasurementVisibilityMode =
    ProfilerMeasurementVisibilityMode.Selected;

profilerWidget
    .SetMeasurementVisible("cycle.cpu.ms", true)
    .SetMeasurementVisible("foreground.cpu.ms", true)
    .SetMeasurementVisible("presentation.count", true)
    .SetMeasurementVisible("build.cpu.ms", true)
    .SetMeasurementVisible("replay.cpu.ms", true);
```

Whole sources can be hidden with `SetSourceVisible`. `ShowUnavailableMeasurements`,
`ShowHeader`, `HeaderText`, `ShowSnapshotMetadata`, `ShowSourceHeaders`,
`RefreshInterval`, font/colors, size, and Z-order are also configurable.

The widget can optionally include non-profiler runtime context without making those
values part of telemetry history:

```csharp
profilerWidget.ContextInfo =
    ProfilerContextInfo.Scene |
    ProfilerContextInfo.View |
    ProfilerContextInfo.Backbuffer |
    ProfilerContextInfo.EngineConfiguration |
    ProfilerContextInfo.Msaa;
```

Applications can append their own lightweight lines at the same refresh cadence.
The callback receives the latest detached snapshot together with the host and view:

```csharp
profilerWidget.AdditionalLinesProvider = context =>
[
    $"Level: {currentLevelName}",
    $"Player state: {player.State}"
];
```

The saved-scene Scene Viewer used by the `.gscn` editor/Studio workflow now dogfoods
this API: its F3 overlay is a `ProfilerWidget` using `ProfilerContextInfo.All`, while
the extension callback supplies the viewer-specific scene/stress and F4 animation-state
lines.

See [[ProfilerWidget|Widgets---ProfilerWidget]] for the complete widget usage notes.

Do not start and dispose the request every time a display refreshes. Each consumer
holds its own request. Hiding a `ProfilerWidget` releases only that widget's request, and hiding the Scene Viewer's F3 diagnostics overlay releases only that Scene Viewer widget's request. The last release stops
collection and publishes the final partial window, if time has elapsed. Engine
disposal invalidates all requests and retires registered sources.
Engine owns the shared collector; application consumers own their collection requests.

## Windows, history, and ownership

The default interval is 250 ms and history retains 120 completed buckets. A bucket
closes on the next observation or query after the interval expires. Its actual
elapsed time is recorded; do not assume exactly 250 ms. Idle time becomes a single
empty window, without a catch-up loop. Supported metrics without observations have
`NotYetSampled` and nullable min/max/mean/last values. A genuinely measured zero is
`Available`. `Unsupported` and `NotApplicable` are distinct states.

Observations are assigned when recorded, usually at work completion. A duration
can span a bucket boundary and is not split between buckets. Reset and final stop
reject in-flight work carrying an earlier generation.

`Reset()` clears telemetry history and counter baselines, advances `Generation`,
and preserves active requests. It does not change renderer mailbox counters.
Restarting after a stopped gap starts another generation and rebases counters;
the gap is excluded from elapsed time. Completed history from previous collection
sessions remains until reset or eviction. Use generation boundaries when comparing
history. Late work from an older generation or a retired source is ignored.

Snapshots use read-only detached collections and contain no Scene, host, drawable,
image, canvas, or GPU-context references. Multiple readers can retain the same
snapshot. No notifications are queued. Short locks protect sufficient statistics;
queries never enumerate live scene collections or take the simulation gate.
Bucket publication allocates at the aggregation cadence; individual observations
retain no raw samples and allocate no managed memory.

Options validate intervals from 10 ms to one minute, history capacities 1–1024,
source capacities 1–64, and per-source metric capacities 8–512. Defaults retain
16 sources and 128 metrics per source. Configuration changes require no active
requests and cannot shrink below registered definitions. Source names, backend
labels, and metric keys are limited to 96 characters. Rejected registrations and
omitted layer detail mark `Truncated`; truncation remains visible for the collector
session. Render detail retains the first eight layer indices, matching the saved-scene Scene Viewer's F3 diagnostics display
limit. `layers.omitted` reports the excess. A refused source returns null and does
not later register itself automatically when another source retires.

## Measurement definitions

All `cpu.ms` values measure elapsed CPU-side work, **not GPU hardware execution**.
Engine and GL observations are asynchronous, not a simultaneous world snapshot.
Each summary includes its count, sum, extrema, mean, last observation, and monotonic
`LastObservedSeconds`. Window timestamps use the same collector clock in seconds.

| Metric | Boundary / interpretation |
| --- | --- |
| `cycle.cpu.ms` | One simulation cycle, from before pre-cycle plugins through post-cycle plugins; excludes scheduling/gate wait and dispatcher draining |
| `background.cpu.ms` | Background work including its before/after event handlers |
| `foreground.cpu.ms` | Foreground work including pre/post frame plugins; nested inside cycles when a cycle renders |
| `build.cpu.ms` | Completed desktop command recording through mailbox publication, or synchronous WebGL scene render |
| `query.cpu.ms`, `sort.cpu.ms`, `record.cpu.ms`, `overlay.cpu.ms` | Parts of the inclusive build total; never add these again to the build total |
| `replay.cpu.ms` | Acquired desktop snapshot replay through backbuffer flush; excludes the following snapshot call |
| `picture.cpu.ms`, `backbuffer.flush.cpu.ms`, `snapshot.cpu.ms` | Replay components and following snapshot call, using existing renderer boundaries |
| `presentation.cpu.ms` | WinForms paint callback CPU duration through final flush and frame accounting; not swap completion |
| `render.snapshot.cpu.ms`, `blit.cpu.ms`, `flush.cpu.ms` | WinForms callback components; inclusive replay appears inside render+snapshot |
| `presentation.count` | Successful adapter callback observations, including re-presentation; independent of new builds |
| `snapshot.age.ms` | Age of the acquired recording at replay acquisition |
| `mailbox.*.lifetime` | Renderer lifetime counters; use Last, or WindowDelta after a baseline has been observed |
| `mailbox.slots`, `snapshot.commands.approximate` | Latest occupancy and approximate Skia command count; these are not GPU draw calls |
| `visible.*`, `atlas.*` | Draw instances across views and atlas operations/tiles, not unique gameplay objects |
| `layer.N.*` | First eight layer indices: query/record CPU timing, visible counts, latest tile dimensions, transformed tiles, and Z order |

Layer indices are observation-time positions, not persistent gameplay-object
identities. Replacing or reordering layers within a window can combine observations
at that index; use a reset when an application needs a clean comparison boundary.

For sample metrics, count divided by **actual window elapsed seconds** gives
cadence. Simulation, foreground, build, and presentation cadences are distinct.
A repeated presentation does not add a build sample. Means across compatible
history windows must be weighted by sample count; `TelemetrySummary.Combine`
does this. It does not combine elapsed time for you. Gauges use Last for current
state; do not sum gauges across history. Lifetime counter deltas are null until a
baseline exists, and reset never writes to the renderer counter.

## Backend support

| Measurement | Bitmap | Desktop GPU | Browser WebGL |
| --- | --- | --- | --- |
| Engine cycle/background/foreground | Supported | Supported | Supported, including timer-driven steps |
| Build/query/record/layers | Unsupported | Supported | Supported synchronously |
| Atlas batching | Unsupported | Supported | Unsupported |
| Immutable-snapshot replay/mailbox | Unsupported | Supported | NotApplicable |
| Live simulation gate timing | Unsupported | NotApplicable for desktop replay | Unsupported |
| Presentation cadence | Unsupported | WinForms and Avalonia | Supported, including re-presentation |
| Callback components | Unsupported | WinForms; Unsupported on Avalonia | Unsupported |

## Legacy CPS/FPS compatibility API

The older CPS/FPS surface remains functional for source and configuration compatibility,
but its public entry points are now warning-only obsolete with diagnostic ID
`GOND0001`:

- `Engine.CyclesPerSecond`
- `Engine.FramesPerSecond`
- `Engine.CPSCalculated`
- `EngineConfiguration.SamplingTimeForCPS`
- `EngineConfiguration.SamplingTimeForCPSTicks`

New code should use `Engine.Profiler` for telemetry or `ProfilerWidget` for an
in-game display. The old `CyclesPerSecondCalculatedEventArgs` DTO itself is not
obsolete so existing compatibility handlers can continue to compile without
additional type-level warning noise.

The conceptual migration is:

| Legacy value | Runtime telemetry equivalent |
| --- | --- |
| Gross CPS / `CyclesPerSecond` | `Engine [Core] / cycle.cpu.ms` sample count divided by snapshot elapsed seconds |
| Net CPS / `FramesPerSecond` | `Engine [Core] / foreground.cpu.ms` sample count divided by snapshot elapsed seconds |
| `GpuFps` | Render-surface `presentation.count` sample count divided by snapshot elapsed seconds |
| `SamplingTimeForCPS` | `RuntimeProfiler.Configure(new TelemetryOptions { Interval = ... })` while collection is inactive |

The compatibility sampler remains independent for now. Profiler collection works
even when legacy CPS sampling is disabled, and profiler requests neither install
legacy event subscriptions nor consume the legacy frame counters. No removal version
is committed by this deprecation.

Render identities last for the host's lifetime, including temporary context loss,
and end at permanent disposal. Existing bitmap rendering and invalidation behavior
are unchanged; no bitmap dirty-area or resource inventory is inferred.

The saved-scene Scene Viewer's **F3 diagnostics overlay**—used when viewing a
`.gscn` from the standalone Scene editor, Studio, or a direct Scene Viewer launch—is
implemented with `ProfilerWidget`. The widget formats snapshots on the Engine thread
at approximately 4 Hz. Its optional runtime-context flags provide camera, dimensions,
configuration, MSAA, and cheap scene/context counts at that cadence; the Scene Viewer
uses the extension callback for its scene/stress and F4 animation-state lines. These
display-context values are not presented as GPU draw calls.

## Small extensions

Register a bounded source and define its metrics once during setup:

```csharp
using var source = Engine.Instance.Profiler.RegisterSource("Pathfinding", "Game");
source?.Define("search.cpu.ms");
long generation = source?.BeginSample() ?? 0;
// Perform work; use your monotonic timer only if generation != 0.
source?.Record(generation, "search.cpu.ms", measuredMilliseconds);
```

Declare units and boundaries in your keys/documentation. Define gauges and lifetime
counters with `TelemetryMetricKind`. Capture the generation **before** work;
recording with a fresh token afterward would incorrectly admit pre-reset work.
Non-finite values and undefined keys are ignored. Dispose source handles when their
owners retire. Definitions may upgrade an unsupported metric to supported when an
adapter attaches; repeated definitions otherwise preserve existing semantics.

See the repository's `docs/ai/runtime-telemetry-395.md` for validation and measured
overhead. Measurements depend on hardware, scene, rendering settings, and display
load; a capped FPS result alone is not an overhead measurement.
