# PopupWidget

`PopupWidget` displays short-lived text or image content that can move, accelerate, fade in, and fade out. It is intended for transient feedback such as damage numbers, healing values, XP gains, status effects, item pickups, or score popups.

A popup can live in either view coordinates or scene-layer world coordinates.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Floating damage text

```csharp
using System.Drawing;
using System.Numerics;
using Gondwana.Widgets.Overlays;
using SkiaSharp;

var damage = new PopupWidget(
        host,
        sceneLayer,
        new Rectangle(200, 180, 96, 36),
        "-25")
    .SetTextColor(SKColors.OrangeRed);

damage.LifetimeSec = 1.0f;
damage.FadeInSec = 0.08f;
damage.FadeOutSec = 0.25f;
damage.VelocityPxPerSec = new Vector2(0f, -64f);

damage.ShowPopup();
```

## Binding to a tile

A scene-layer popup can resolve its starting point from a tile when it is shown:

```csharp
var damage = new PopupWidget(
        host,
        targetTile,
        new Size(96, 36),
        "-25",
        WidgetAnchor.TopCenter)
    .SetTextColor(SKColors.OrangeRed);

damage.ShowPopup();
```

You can also bind to a fixed grid coordinate:

```csharp
damage.BindTo(
    sceneLayer,
    new Point(12, 8),
    WidgetAnchor.Center);
```

The source is resolved when `ShowPopup()` starts. After that, the popup moves independently from the source.

## Acceleration

```csharp
damage.VelocityPxPerSec = new Vector2(0f, -80f);
damage.AccelerationPxPerSecSquared = new Vector2(0f, 30f);
```

## Image popups

`PopupWidget` also accepts `SKImage` or `SKBitmap` content:

```csharp
var iconPopup = new PopupWidget(
    host,
    view,
    new Rectangle(300, 80, 48, 48),
    pickupImage);

iconPopup.ShowPopup();
```

## Completion and lifetime

```csharp
damage.Completed += () =>
{
    // Optional follow-up work.
};

damage.DisposeOnComplete = true;
```

Call `Dismiss()` to end it immediately.

## Useful members

| Member | Purpose |
| --- | --- |
| `LifetimeSec` | Total popup lifetime. |
| `FadeInSec` / `FadeOutSec` | Fade durations. |
| `VelocityPxPerSec` | Initial movement. |
| `AccelerationPxPerSecSquared` | Movement acceleration. |
| `BindTo()` | Resolves source from a tile or grid cell. |
| `ShowPopup()` | Starts the popup lifecycle. |
| `Dismiss()` | Completes it immediately. |
| `Completed` | Raised when the popup ends. |
