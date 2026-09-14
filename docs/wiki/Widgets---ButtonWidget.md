# ButtonWidget

`ButtonWidget` is the standard clickable text button in `Gondwana.Widgets`. It is appropriate for menu commands, dialog actions, HUD controls, and any other in-game action that should respond to pointer clicks or keyboard activation.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Basic use

```csharp
using System.Drawing;
using Gondwana.Widgets.Controls;

var startButton = new ButtonWidget(
    host,
    view,
    new Rectangle(40, 40, 180, 42),
    "Start Game");

startButton.Clicked += () =>
{
    StartGame();
};
```

The button can receive focus and can be activated with the primary pointer button, **Enter**, or **Space**.

## Styling

```csharp
startButton
    .SetBackgroundColors(
        Color.FromArgb(255, 45, 80, 120),
        Color.FromArgb(255, 60, 105, 155),
        Color.FromArgb(255, 30, 60, 95))
    .SetTextColor(Color.White)
    .SetButtonZOrder(100);
```

`SetText()` changes the caption after creation:

```csharp
startButton.SetText("Resume Game");
```

You can also invoke the button in code:

```csharp
startButton.PerformClick();
```

`PerformClick()` respects `IsInputEnabled`; if input is disabled, the click is not raised.

## View or SceneLayer

A button can be attached either to a `View` or to a `SceneLayer`:

```csharp
var worldButton = new ButtonWidget(
    host,
    sceneLayer,
    new Rectangle(200, 120, 140, 36),
    "Open");
```

Use the **View** form for normal screen-space UI. Use the **SceneLayer** form when the control should live in world coordinates and move with that layer.

## Useful members

| Member | Purpose |
| --- | --- |
| `Clicked` | Raised when the button is activated. |
| `SetText()` | Changes the button caption. |
| `SetBackgroundColors()` | Sets normal, hover, and pressed colors. |
| `SetTextColor()` | Changes the caption color. |
| `SetButtonZOrder()` | Places the label immediately above the button background. |
| `Background` | Access to the underlying `DirectRectangle`. |
| `Label` | Access to the underlying `TextBlock`. |
