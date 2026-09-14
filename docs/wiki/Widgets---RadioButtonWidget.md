# RadioButtonWidget

`RadioButtonWidget` represents a mutually exclusive choice. Several radio buttons can share a `RadioButtonGroup`, ensuring that selecting one clears the others.

Use it when the player must choose exactly one option from a small visible set.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Creating a group

```csharp
using System.Drawing;
using Gondwana.Widgets.Controls;

var displayModeGroup = new RadioButtonGroup();

var windowed = new RadioButtonWidget(
    host,
    view,
    new Rectangle(40, 40, 220, 28),
    "Windowed",
    displayModeGroup,
    isSelected: true);

var fullscreen = new RadioButtonWidget(
    host,
    view,
    new Rectangle(40, 74, 220, 28),
    "Fullscreen",
    displayModeGroup);
```

Selecting one button automatically clears the other:

```csharp
fullscreen.Select();

RadioButtonWidget? selected = displayModeGroup.SelectedButton;
```

## Responding to selection

```csharp
fullscreen.SelectionChanged += isSelected =>
{
    if (isSelected)
        videoSettings.Fullscreen = true;
};
```

The widget can be selected with the primary pointer button or **Space** while focused.

## Styling

```csharp
fullscreen
    .SetColors(
        normal: Color.FromArgb(255, 48, 48, 56),
        hover: Color.FromArgb(255, 70, 70, 82),
        dot: Color.Gold)
    .SetTextColor(SkiaSharp.SKColors.White)
    .SetRadioButtonZOrder(40);
```

## Ungrouped radio buttons

The `group` argument is optional. An ungrouped radio button can still be selected, but nothing else is automatically cleared.

## Useful members

| Member | Purpose |
| --- | --- |
| `Group` | Gets or changes the coordinating group. |
| `IsSelected` | Gets or sets the selected state. |
| `Select()` | Selects this button. |
| `SelectionChanged` | Raised when its state changes. |
| `Ring` / `Dot` / `Label` | Underlying visuals. |

Both view-level and scene-layer constructors are available.
