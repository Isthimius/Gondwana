# NameTagWidget

`NameTagWidget` displays a world-space text label above a `Sprite` and follows that sprite automatically.

Use it for NPC names, player names, unit labels, merchant names, quest-giver labels, or similar entity identification.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Basic use

```csharp
using System.Drawing;
using Gondwana.Widgets.Hud;

var nameTag = new NameTagWidget(
    host,
    sprite,
    "Merchant",
    size: new Size(120, 26));
```

As the sprite moves—or its visual bounds change—the tag updates its position automatically.

## Updating the name

```csharp
nameTag.SetText("Master Merchant");
```

## Position and size

```csharp
nameTag.OffsetPx = new Point(0, -4);
nameTag.Size = new Size(150, 28);
```

## Styling

```csharp
nameTag
    .SetTextColor(SkiaSharp.SKColors.Gold)
    .SetBackgroundColor(Color.FromArgb(180, 20, 22, 28))
    .SetNameTagZOrder(20);
```

For text without a box:

```csharp
nameTag.ShowBackground(false);
```

## Lifetime

`NameTagWidget` subscribes to the target sprite's movement, visual-bounds, and disposal events. Disposing the sprite automatically disposes the name tag.

## Useful members

| Member | Purpose |
| --- | --- |
| `Target` | Sprite being followed. |
| `Text` | Current name. |
| `SetText()` | Changes the displayed name. |
| `Size` | Tag size in world pixels. |
| `OffsetPx` | Additional offset above the sprite. |
| `BoundsWorld` | Current world-space bounds. |
| `ShowBackground()` | Shows or hides the backing rectangle. |
| `Background` / `TextBlock` | Underlying visuals. |
