# HealthBarWidget

`HealthBarWidget` is a world-space health bar that automatically follows a `Sprite`. Unlike `ProgressBarWidget`, it works with a real maximum/current health pair and is designed specifically for game entities.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Basic use

```csharp
using System.Drawing;
using Gondwana.Widgets.Hud;

var healthBar = new HealthBarWidget(
    host,
    sprite,
    maximum: 100f,
    size: new Size(72, 10));

healthBar.Value = 75f;
```

The widget automatically repositions itself as the sprite moves.

## Updating health

```csharp
spriteHealth -= damage;
healthBar.Value = spriteHealth;
```

Values are clamped between `0` and `Maximum`.

```csharp
float percent = healthBar.Fraction;
```

## Maximum health changes

```csharp
healthBar.Maximum = 150f;
healthBar.Value = 150f;
```

## Position and appearance

```csharp
healthBar.OffsetPx = new Point(0, -4);

healthBar
    .SetFillColor(Color.OrangeRed)
    .SetValue(90f);
```

You normally do not need to reposition it manually, but `RefreshPosition()` is available if some external visual change requires an immediate refresh.

## Lifetime

The widget listens to its target sprite. When the sprite is disposed, the health bar disposes itself as well.

## Useful members

| Member | Purpose |
| --- | --- |
| `Target` | Sprite being followed. |
| `Maximum` / `Value` | Health range and current health. |
| `Fraction` | Current normalized health from 0 through 1. |
| `Size` | Bar dimensions in world pixels. |
| `OffsetPx` | Additional offset from the normal centered-above-sprite position. |
| `TrackBoundsWorld` / `FillBoundsWorld` | Current world bounds. |
| `SetFillColor()` | Changes the health fill color. |
