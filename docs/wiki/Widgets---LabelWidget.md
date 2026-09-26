# LabelWidget

`LabelWidget` is the general-purpose retained-mode text widget. Use it for captions, headings, status text, score displays, instructions, and other non-editable text.

By default it remains non-interactive. Optional vertical scrolling can be enabled for longer read-only text; in that mode the label accepts pointer input for the mouse wheel, scrollbar track, and draggable thumb while remaining non-focusable.

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

## Optional vertical scrolling

Labels keep their traditional non-interactive behavior unless scrolling is enabled.

```csharp
var helpText = new LabelWidget(
        host,
        view,
        new Rectangle(40, 80, 480, 320),
        longInstructions)
    .SetFont(SKTypeface.Default, 17f)
    .SetAlignment(
        SKTextAlign.Left,
        Gondwana.Drawing.Direct.TextBlock.VerticalAlign.Top)
    .EnableWrapping()
    .SetPadding(horizontal: 8f, vertical: 6f);

helpText.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
helpText.MouseWheelScrollPixels = 48;
```

`VerticalScrollBarVisibility` supports `Auto`, `Always`, and `Never`. The default is `Never`. When visible, the scrollbar reserves a narrow gutter so wrapped text does not render underneath it.

Users can scroll with the mouse wheel, click above or below the thumb to page, or drag the thumb directly. `VerticalScrollOffsetPx` can also be set programmatically and clamps to `MaximumVerticalScrollOffsetPx`.

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
| `VerticalScrollBarVisibility` | Controls `Auto`, `Always`, or `Never` scrollbar display; defaults to `Never`. |
| `MouseWheelScrollPixels` | Number of native pixels scrolled per wheel notch. |
| `VerticalScrollOffsetPx` | Current content offset. |
| `MaximumVerticalScrollOffsetPx` | Maximum valid content offset for the current layout. |
| `IsVerticalScrollBarVisible` | Reports whether the scrollbar is currently shown. |
| `VerticalScrollBarTrack` / `VerticalScrollBarThumb` | Access to the retained scrollbar drawings. |
| `SetLabelZOrder()` | Sets the label text Z-order and keeps the scrollbar above it. |
| `TextBlock` | Access to the underlying `TextBlock`. |

`LabelWidget` supports both view-level and scene-layer text.
