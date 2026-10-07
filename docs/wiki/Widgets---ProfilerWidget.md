# ProfilerWidget

`ProfilerWidget` is a view-level HUD for displaying the opt-in runtime telemetry
collected by `Engine.Instance.Profiler`. It is intended for development and
diagnostic builds where an ordinary game needs the same underlying measurements
available to the saved-scene Scene Viewer's F3 diagnostics overlay. That F3 binding
belongs to the Scene Viewer used by the `.gscn` editor/Studio workflow (or launched
directly); it is not a general Engine hotkey.

The widget lives in:

```csharp
using Gondwana.Widgets.Hud;
```

## Basic use

```csharp
using System.Drawing;
using Gondwana.Widgets.Hud;

var profiler = new ProfilerWidget(
    host,
    view,
    new Rectangle(12, 12, 700, 520));

profiler.Show();
```

`Show()` acquires one independent profiler collection request. `Hide()` releases
that request, and `Dispose()` also releases it if the widget is still active.
Constructing the widget alone does not keep telemetry collection enabled.

The default display:

- shows sampled measurements from every visible profiler source;
- hides `Unsupported`, `NotApplicable`, and `NotYetSampled` rows;
- formats samples as average/minimum/maximum/count/rate;
- formats gauges as their latest value;
- formats lifetime counters as latest value plus window delta;
- shows snapshot generation/window metadata;
- refreshes the display every 250 ms; and
- enables a vertical scrollbar when the text exceeds the widget bounds.

The 250 ms widget refresh interval is only a presentation cadence. It does not cause
the profiler to poll the engine every 250 ms. Measurements are still recorded at
their normal instrumentation boundaries.

## Measurement visibility

Hide or show a metric key across every source:

```csharp
profiler.SetMeasurementVisible("snapshot.age.ms", false);
profiler.SetMeasurementVisible("cycle.cpu.ms", true);
```

Use the source overload when a key should be treated differently for one source:

```csharp
profiler.SetMeasurementVisible(
    "Render surface",
    "snapshot.commands.approximate",
    false);
```

Source-specific overrides take precedence over global metric overrides.

To build an allow-list instead of starting from every sampled measurement:

```csharp
profiler.MeasurementVisibilityMode =
    ProfilerMeasurementVisibilityMode.Selected;

profiler
    .SetMeasurementVisible("cycle.cpu.ms", true)
    .SetMeasurementVisible("foreground.cpu.ms", true)
    .SetMeasurementVisible("presentation.count", true)
    .SetMeasurementVisible("build.cpu.ms", true)
    .SetMeasurementVisible("replay.cpu.ms", true);
```

Hide an entire source:

```csharp
profiler.SetSourceVisible("Pathfinding", false);
```

Call `ClearVisibilityOverrides()` to remove explicit source and metric overrides.
`IsMeasurementVisible(sourceName, metricKey)` reports the current selection policy
for an otherwise available measurement.

## Display options

The following properties control presentation:

| Option | Default | Meaning |
| --- | --- | --- |
| `RefreshInterval` | 250 ms | How often the widget reads/formats the latest completed snapshot; valid range 10 ms–1 minute |
| `MeasurementVisibilityMode` | `All` | Show all otherwise-eligible metrics or only explicitly selected ones |
| `ShowUnavailableMeasurements` | `false` | Include unsupported/not-applicable/not-yet-sampled rows |
| `ShowHeader` | `true` | Show the profiler heading |
| `HeaderText` | `Gondwana Runtime Profiler` | Customize the profiler heading |
| `ShowSnapshotMetadata` | `true` | Show generation, window duration, and truncation state |
| `ShowSourceHeaders` | `true` | Show source name/backend headings |
| `Size` | constructor bounds | Resize while preserving the widget position |

The fluent `SetColors`, `SetFont`, and `SetProfilerZOrder` methods adjust the
visual presentation. `Display` exposes the underlying `LabelWidget` for advanced
label/scrollbar customization.

## Optional runtime context

Profiler snapshots intentionally contain neutral detached measurements rather than
live Scene/View objects. For a diagnostic HUD, `ProfilerWidget` can optionally add
cheap current runtime context at display-refresh cadence:

```csharp
profiler.ContextInfo =
    ProfilerContextInfo.Scene |
    ProfilerContextInfo.View |
    ProfilerContextInfo.Backbuffer |
    ProfilerContextInfo.EngineConfiguration |
    ProfilerContextInfo.Msaa;
```

`ProfilerContextInfo.All` enables all of those sections. The default is
`ProfilerContextInfo.None`, so creating a normal profiler HUD does not add scene,
camera, backbuffer, configuration, or MSAA inspection.

The context sections can report:

- active animating tiles, Scene layer count, and total grid-cell count;
- camera position, viewport zoom, and viewport dimensions;
- logical backbuffer dimensions;
- target FPS and VSync; and
- requested, actual, and maximum MSAA values for GPU backbuffers.

These are display-time values, not telemetry samples and not retained in profiler
history.

## Application-specific extension lines

`AdditionalLinesProvider` is an optional extension point for application/tool-specific
output. It receives a `ProfilerWidgetExtensionContext` containing the latest detached
snapshot, render-surface host, and View:

```csharp
profiler.AdditionalLinesProvider = context =>
[
    $"Level: {currentLevelName}",
    $"Player state: {player.State}"
];
```

The callback runs at the widget refresh cadence, normally on the Engine thread, so it
should remain lightweight.

The saved-scene Scene Viewer is the first built-in dogfood consumer of this extension.
Its F3 diagnostics overlay sets `ContextInfo = ProfilerContextInfo.All` and uses
`AdditionalLinesProvider` for only the viewer-specific Scene/Stress and F4
animation-state lines. All profiler measurements and generic runtime context remain
owned by `ProfilerWidget`.

## Performance and ownership

The widget does not walk live scenes or renderer object graphs. It calls
`GetLatestSnapshot()`, which returns detached bounded telemetry, and formats that
data at the widget refresh cadence.

Each widget owns exactly one collection request while shown. Multiple
`ProfilerWidget` instances, the saved-scene Scene Viewer's F3 diagnostics overlay, or application code can collect
simultaneously; collection stops only after the final request is released.

For measurement definitions, backend support, aggregation behavior, and measured
overhead, see [[Runtime Telemetry]]. For broader debugging guidance, see
[[Debugging and Instrumentation]].
