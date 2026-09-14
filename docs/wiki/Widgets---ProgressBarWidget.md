# ProgressBarWidget

`ProgressBarWidget` displays a normalized value from `0.0` through `1.0`. It is a generic progress indicator rather than a health-specific control.

Use it for loading progress, stamina, mana, cooldowns, capture progress, crafting progress, or any other fractional value.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Basic use

```csharp
using System.Drawing;
using Gondwana.Widgets.Controls;

var loading = new ProgressBarWidget(
    host,
    view,
    new Rectangle(40, 40, 300, 22),
    value: 0.25f);

loading.Value = 0.50f;
```

Values are clamped to the range `0` through `1`.

## Vertical progress

```csharp
using Gondwana.Widgets.Layout;

var meter = new ProgressBarWidget(
    host,
    view,
    new Rectangle(20, 20, 24, 180),
    value: 0.75f,
    orientation: WidgetOrientation.Vertical);
```

Vertical bars fill from **bottom to top**.

## Styling

```csharp
loading
    .SetTrackColor(Color.FromArgb(220, 25, 25, 30))
    .SetFillColor(Color.CornflowerBlue)
    .SetProgressZOrder(50);

loading.Padding = 3;
```

## Useful members

| Member | Purpose |
| --- | --- |
| `Value` | Normalized value from 0 through 1. |
| `Orientation` | Horizontal or vertical. |
| `Padding` | Inner gap between track and fill. |
| `Size` | Outer dimensions. |
| `Track` / `Fill` | Underlying rectangles. |
| `TrackBounds` / `FillBounds` | Current native-coordinate bounds. |
| `SetValue()` | Fluent value setter. |

For sprite health, prefer `HealthBarWidget`, which follows a sprite and accepts real health values rather than a normalized fraction.
