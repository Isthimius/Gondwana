# PanelWidget

`PanelWidget` is a rectangular container for grouping other widgets. It can use a solid color or image background and can provide a border and rounded corners.

Use it for HUD panels, option panes, inventory windows, sidebars, cards, and other grouped interface areas.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Basic use

```csharp
using System.Drawing;
using Gondwana.Widgets.Controls;

var panel = new PanelWidget(
    host,
    view,
    new Rectangle(30, 30, 360, 220),
    Color.FromArgb(220, 24, 26, 32));

var title = new LabelWidget(
    host,
    view,
    new Rectangle(0, 0, 320, 36),
    "Options");

panel.AddWidget(title, new Point(20, 16));
```

Child positions passed to `AddWidget()` are local offsets from the panel's upper-left corner.

## Styling

```csharp
panel
    .SetBorderColor(Color.FromArgb(255, 150, 150, 165))
    .SetStrokeWidth(2f)
    .SetCornerRadius(8f)
    .SetPanelZOrder(100);
```

## Image backgrounds

```csharp
panel.SetBackgroundImage(
    panelBitmap,
    Gondwana.Drawing.Direct.DirectRectangle.ImageFillMode.Repeat,
    filterQuality: SkiaSharp.SKFilterQuality.None);
```

Available `DirectRectangle.ImageFillMode` values include the modes supported by `DirectRectangle`, such as stretch, fit, fill, center, pixel-perfect, and repeat.

Return to the solid fill with:

```csharp
panel.ClearBackgroundImage();
```

## Managing children

```csharp
panel.AddWidget(applyButton, new Point(220, 170));

panel.RemoveWidget(applyButton, dispose: false);
```

Removing a child does not dispose it unless requested.

## Useful members

| Member | Purpose |
| --- | --- |
| `AddWidget()` | Adds a widget at a local panel offset. |
| `RemoveWidget()` | Removes a child, optionally disposing it. |
| `SetBackgroundColor()` | Sets solid fill color. |
| `SetBackgroundImage()` | Uses an `SKBitmap` or `SKImage` background. |
| `SetBorderColor()` / `SetStrokeWidth()` | Configures the border. |
| `SetCornerRadius()` | Rounds the corners. |
| `Size` | Gets or sets panel dimensions. |
| `Background` | Underlying `DirectRectangle`. |

`PanelWidget` can be created in either a `View` or a `SceneLayer`.
