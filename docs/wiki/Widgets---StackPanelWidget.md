# StackPanelWidget

`StackPanelWidget` is a lightweight layout container that arranges child widgets sequentially in a vertical or horizontal line.

Use it for option groups, button rows, toolbar-like groups, vertical menus that are not using the menu subsystem, and other simple code-first layouts.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Vertical stack

```csharp
using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Layout;

var stack = new StackPanelWidget(
    host,
    DirectDrawingMode.View,
    anchor: new PointF(40, 40),
    orientation: WidgetOrientation.Vertical,
    spacing: 8f);

stack
    .AddWidget(new ButtonWidget(
        host, view, new Rectangle(0, 0, 200, 38), "New Game"))
    .AddWidget(new ButtonWidget(
        host, view, new Rectangle(0, 0, 200, 38), "Options"))
    .AddWidget(new ButtonWidget(
        host, view, new Rectangle(0, 0, 200, 38), "Quit"));
```

The stack determines child offsets from their sizes; the original absolute positions are not used as spacing between children.

## Horizontal stack

```csharp
stack.Orientation = WidgetOrientation.Horizontal;
stack.Spacing = 12f;
stack.Relayout();
```

Changing `Orientation` or `Spacing` automatically recalculates layout; calling `Relayout()` explicitly is useful after changing child sizes.

## Content size

```csharp
SizeF size = stack.ContentSize;
```

`ContentSize` reports the current laid-out dimensions of all children including spacing.

## Removing a child

```csharp
stack.RemoveWidget(optionsButton, dispose: false);
```

## Drawing mode

The constructor accepts `DirectDrawingMode.View` or `DirectDrawingMode.SceneLayer`. The first child establishes the actual view or scene-layer target used by the container.

Do not mix view-level and scene-layer widgets in the same stack.

## Useful members

| Member | Purpose |
| --- | --- |
| `Orientation` | Horizontal or vertical layout. |
| `Spacing` | Pixels between adjacent children. |
| `ContentSize` | Current total laid-out size. |
| `AddWidget()` | Adds and lays out a child. |
| `RemoveWidget()` | Removes a child and recalculates layout. |
| `Relayout()` | Recomputes all child offsets. |
