# CheckBoxWidget

`CheckBoxWidget` represents an independent two-state choice such as **Music enabled**, **Show FPS**, or **Remember this setting**.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Basic use

```csharp
using System.Drawing;
using Gondwana.Widgets.Controls;

var musicCheckBox = new CheckBoxWidget(
    host,
    view,
    new Rectangle(40, 40, 220, 28),
    "Music",
    isChecked: true);

musicCheckBox.CheckedChanged += isChecked =>
{
    audioSettings.MusicEnabled = isChecked;
};
```

The widget toggles when clicked or when **Space** is pressed while it has focus.

## Reading and changing the state

```csharp
bool enabled = musicCheckBox.IsChecked;

musicCheckBox.IsChecked = false;

// Equivalent fluent methods:
musicCheckBox.SetChecked(true);
musicCheckBox.Toggle();
```

## Styling

```csharp
musicCheckBox
    .SetColors(
        normal: Color.FromArgb(255, 48, 48, 56),
        hover: Color.FromArgb(255, 70, 70, 82),
        mark: Color.LimeGreen)
    .SetTextColor(SkiaSharp.SKColors.White)
    .SetCheckBoxZOrder(50);
```

## View or SceneLayer

`CheckBoxWidget` has both `View` and `SceneLayer` constructors. For settings screens and HUDs, the view-level form is normally the right choice. The scene-layer form is useful when the checkbox is intentionally part of a world-space interface.

## Useful members

| Member | Purpose |
| --- | --- |
| `IsChecked` | Gets or sets the current state. |
| `CheckedChanged` | Raised when the state changes. |
| `Toggle()` | Flips the current state. |
| `SetText()` | Changes the label. |
| `Box` | The outer checkbox rectangle. |
| `Mark` | The filled checked-state mark. |
| `Label` | The associated `TextBlock`. |
