# TooltipWidget

`TooltipWidget` is a lightweight, view-level overlay for contextual help near the pointer.

It is intentionally non-interactive: the tooltip itself does not intercept mouse, touch,
or keyboard input from the control or game content underneath it.

## Basic use

```csharp
using Gondwana.Widgets.Overlays;

var tooltip = new TooltipWidget(
    host,
    view,
    new Size(320, 72));

tooltip.ShowTooltip(
    "query.cpu.ms\nTime spent querying and culling visible drawables.",
    pointerPosition);
```

Hide it when the pointer leaves the relevant target:

```csharp
tooltip.HideTooltip();
```

`ShowTooltip` positions the overlay near the supplied screen coordinate, flips it to the
opposite side of the pointer when necessary, and clamps it to the owning View.

## Options

- `Size` controls the tooltip bounds.
- `PointerOffsetPx` controls the gap from the pointer.
- `SetTooltipZOrder(...)` controls overlay ordering.
- `Background` exposes the underlying `DirectRectangle`.
- `Label` exposes the underlying `TextBlock`.

## ProfilerWidget integration

`ProfilerWidget` uses `TooltipWidget` to show measurement definitions when the pointer
hovers a rendered diagnostic line.

That behavior can be toggled at runtime:

```csharp
profiler.MeasurementTooltipsEnabled = false;
```

The built-in **Metrics...** selector also contains a **Hover definitions** toggle.

Custom profiler measurements can provide their own hover definition:

```csharp
profiler.SetMeasurementDescription(
    "pathfinding.cpu.ms",
    "CPU time spent updating the pathfinding system.");
```
