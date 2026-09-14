# LabelWidget

`LabelWidget` is the general-purpose retained-mode text widget. Use it for captions, headings, status text, score displays, instructions, and other non-editable text.

It intentionally does not receive pointer or keyboard input.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Basic use

```csharp
using System.Drawing;
using Gondwana.Widgets.Controls;
using SkiaSharp;

var scoreLabel = new LabelWidget(
    host,
    view,
    new Rectangle(20, 20, 240, 36),
    "Score: 0");

scoreLabel
    .SetFont(SKTypeface.Default, 20f, minSize: 12f)
    .SetTextColor(SKColors.White)
    .SetAlignment(
        SKTextAlign.Left,
        Gondwana.Drawing.Direct.TextBlock.VerticalAlign.Center);
```

Update it as the game changes:

```csharp
scoreLabel.SetText($"Score: {score}");
```

## Wrapping and padding

```csharp
var instructions = new LabelWidget(
        host,
        view,
        new Rectangle(40, 100, 420, 120),
        "Use the arrow keys to move. Find the key and unlock the gate.")
    .EnableWrapping()
    .SetPadding(horizontal: 8f, vertical: 6f);
```

## Colors

```csharp
instructions.SetColors(
    foreground: SKColors.White,
    background: new SKColor(0, 0, 0, 160));
```

## Resizing

```csharp
scoreLabel.Size = new Size(320, 36);
```

## Useful members

| Member | Purpose |
| --- | --- |
| `Text` | Current displayed text. |
| `SetText()` | Replaces the text. |
| `SetFont()` | Configures typeface and font size. |
| `SetColors()` | Sets foreground and background colors. |
| `SetAlignment()` | Configures horizontal and vertical alignment. |
| `EnableWrapping()` | Enables or disables wrapping. |
| `SetPadding()` | Sets horizontal and vertical padding. |
| `TextBlock` | Access to the underlying `TextBlock`. |

`LabelWidget` supports both view-level and scene-layer text.
