# Runtime telemetry

`Engine.Instance.Profiler` provides opt-in CPU measurements without SceneViewer or
Studio. It is disabled until an application or F3 holds a collection request.

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

Do not start and dispose the request every time a display refreshes. Each consumer
holds its own request. Hiding F3 releases only F3's request. The last release stops
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
session. Render detail retains the first eight layer indices, matching F3's display
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

Legacy `CPSCalculated` and render diagnostic events retain their independent
behavior. Profiler collection works when `SamplingTimeForCPS` is zero. A profiler
request does not install event subscriptions or consume legacy frame counters.
Render identities last for the host's lifetime, including temporary context loss,
and end at permanent disposal. Existing bitmap rendering and invalidation behavior
are unchanged; no bitmap dirty-area or resource inventory is inferred.

F3 formats snapshots on the Engine thread at approximately 4 Hz. Its camera,
dimensions, configuration, MSAA, and cheap scene/context counts are captured there
at that cadence. It preserves white text, black background alpha 102, and F4
animation pause. These display-context values are not presented as GPU draw calls.

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
