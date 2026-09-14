> A compact, code-first reference for mouse clicks, movement, dragging, and wheel input in Gondwana.

---

## Contents

- [The Basic Pattern](#the-basic-pattern)
- [Show a Left Mouse Click](#show-a-left-mouse-click)
- [Show a Right Mouse Click](#show-a-right-mouse-click)
- [Track Pointer Movement](#track-pointer-movement)
- [Implement a Drag](#implement-a-drag)
- [Read the Scroll Wheel](#read-the-scroll-wheel)
- [Convert the Pointer to World Coordinates](#convert-the-pointer-to-world-coordinates)
- [Use Modifier Keys](#use-modifier-keys)
- [Control Event Frequency](#control-event-frequency)
- [Cleanup](#cleanup)
- [Cheat Sheet](#cheat-sheet)
- [Common Problems](#common-problems)
- [Further Reading](#further-reading)

---

# The Basic Pattern

Mouse input uses one consolidated event:

```text
Initialize adapter
    ↓
Subscribe to MouseEvent
    ↓
Start monitoring
    ↓
Inspect position, buttons, wheel, and modifiers
```

In a Gondwana game host:

```csharp
protected override void OnMouseAdapterInitialized()
{
    var mouse = Engine.Input.MouseEventPoller;
    if (mouse is null)
        return;

    mouse.MouseEvent += OnMouseEvent;

    mouse.StartMonitoringMouse(
        trackMouseMovement: true);
}
```

---

# Show a Left Mouse Click

```csharp
using Gondwana.Input.Mouse;
```

```csharp
private void OnMouseEvent(MouseEventArgs e)
{
    if (e.LeftButtonJustPressed)
    {
        Console.WriteLine(
            $"Left mouse pressed at {e.CurrentPosition}.");
    }
}
```

`LeftButtonJustPressed` is true only when the button transitions from up to down.

To act when the button is released:

```csharp
if (e.LeftButtonJustReleased)
{
    Console.WriteLine(
        $"Left mouse released at {e.CurrentPosition}.");
}
```

---

# Show a Right Mouse Click

```csharp
private void OnMouseEvent(MouseEventArgs e)
{
    if (e.RightButtonJustPressed)
    {
        OpenContextMenu(
            e.CurrentPosition);
    }
}
```

Middle-button helpers are available too:

```csharp
e.MiddleButtonDown
e.MiddleButtonJustPressed
e.MiddleButtonJustReleased
```

Or use the generic form:

```csharp
if (e.IsButtonJustPressed(MouseButton.Middle))
{
}
```

---

# Track Pointer Movement

```csharp
private void OnMouseEvent(MouseEventArgs e)
{
    if (e.CurrentPosition == e.PreviousPosition)
        return;

    int dx =
        e.CurrentPosition.X -
        e.PreviousPosition.X;

    int dy =
        e.CurrentPosition.Y -
        e.PreviousPosition.Y;

    Console.WriteLine(
        $"Mouse moved by ({dx}, {dy}).");
}
```

Make sure movement tracking is enabled:

```csharp
mouse.StartMonitoringMouse(
    trackMouseMovement: true);
```

---

# Implement a Drag

```csharp
private Point? _dragStart;
```

```csharp
private void OnMouseEvent(MouseEventArgs e)
{
    if (e.LeftButtonJustPressed)
    {
        _dragStart = e.CurrentPosition;
        return;
    }

    if (e.LeftButtonDown &&
        _dragStart is { } start)
    {
        var delta = new Point(
            e.CurrentPosition.X - start.X,
            e.CurrentPosition.Y - start.Y);

        DragSelection(
            start,
            e.CurrentPosition,
            delta);

        return;
    }

    if (e.LeftButtonJustReleased)
    {
        _dragStart = null;
    }
}
```

For Gondwana widgets, prefer the widget input router and widget drag events. Use raw mouse dragging for world interaction, camera controls, editors, and custom tools.

---

# Read the Scroll Wheel

```csharp
private void OnMouseEvent(MouseEventArgs e)
{
    if (e.ScrollDelta > 0)
    {
        ZoomIn();
    }
    else if (e.ScrollDelta < 0)
    {
        ZoomOut();
    }
}
```

The magnitude is adapter-defined. On WinForms it commonly arrives in wheel-detent-sized increments.

For smooth zoom, convert the delta into a target and let the view animate:

```csharp
if (e.ScrollDelta != 0)
{
    var view =
        RenderSurface.Host.ViewManager.Views[0];

    var layer = Scene![0];

    float targetZoom = Math.Clamp(
        view.Viewport.Zoom +
        e.ScrollDelta * 0.001f,
        view.MinZoom,
        view.MaxZoom);

    view.ZoomAroundScreenPoint(
        layer,
        e.CurrentPosition,
        targetZoom,
        0.25f);
}
```

---

# Convert the Pointer to World Coordinates

The mouse reports render-surface coordinates. Convert through the intended `View`.

```csharp
private void OnMouseEvent(MouseEventArgs e)
{
    var view =
        RenderSurface.Host.ViewManager.Views[0];

    var layer = Scene![0];

    PointF worldPx =
        view.ScreenPxToWorldPx(
            layer,
            e.CurrentPosition);

    Console.WriteLine(
        $"World pixel: {worldPx}");
}
```

Convert directly to the layer's grid:

```csharp
PointF grid =
    view.ScreenPxToGrid(
        layer,
        e.CurrentPosition);
```

Select a tile:

```csharp
var tile = layer[grid];

if (tile is not null &&
    e.LeftButtonJustPressed)
{
    SelectTile(tile);
}
```

---

# Use Modifier Keys

Mouse events include keyboard modifiers:

```csharp
e.IsShift
e.IsCtrl
e.IsAlt
```

Example:

```csharp
if (e.LeftButtonJustPressed)
{
    if (e.IsCtrl)
        AddToSelection(e.CurrentPosition);
    else
        ReplaceSelection(e.CurrentPosition);
}
```

---

# Control Event Frequency

Use the engine default:

```csharp
mouse.StartMonitoringMouse(
    trackMouseMovement: true,
    timeBetweenEvents: -1);
```

No added throttle:

```csharp
mouse.StartMonitoringMouse(
    trackMouseMovement: true,
    timeBetweenEvents: 0);
```

Disable free movement events:

```csharp
mouse.StartMonitoringMouse(
    trackMouseMovement: false);
```

Pause:

```csharp
mouse.Configuration!.IsPaused = true;
```

Resume:

```csharp
mouse.Configuration!.IsPaused = false;
```

Stop monitoring:

```csharp
mouse.StopMonitoringMouse();
```

---

# Cleanup

```csharp
protected override void UnhookEvents()
{
    if (Engine.Input.MouseEventPoller is { } mouse)
        mouse.MouseEvent -= OnMouseEvent;
}
```

---

# Cheat Sheet

## Left press

```csharp
if (e.LeftButtonJustPressed)
{
}
```

## Left held

```csharp
if (e.LeftButtonDown)
{
}
```

## Left release

```csharp
if (e.LeftButtonJustReleased)
{
}
```

## Pointer position

```csharp
Point p = e.CurrentPosition;
```

## Movement delta

```csharp
int dx =
    e.CurrentPosition.X -
    e.PreviousPosition.X;
```

## Wheel

```csharp
if (e.ScrollDelta != 0)
{
}
```

## Screen to world

```csharp
PointF world =
    view.ScreenPxToWorldPx(
        layer,
        e.CurrentPosition);
```

---

# Common Problems

## Clicks work, but hover does not

Enable pointer movement tracking:

```csharp
mouse.StartMonitoringMouse(
    trackMouseMovement: true);
```

## The event fires repeatedly during a drag

That is expected while a button remains held. Use:

```csharp
JustPressed
JustReleased
```

for edge-only behavior.

## The pointer selects the wrong world location

Use the correct `View` and `SceneLayer` when converting screen coordinates.

## Input feels delayed

Reduce `timeBetweenEvents`. A value of `0` removes interval throttling, but it also increases event frequency.

## A UI control must be changed

Mouse handlers normally run in the engine cycle. Dispatch platform UI changes:

```csharp
Engine.UiDispatcher?.Post(() =>
{
    label.Text = "Clicked";
});
```

---

# Further Reading

- [[Understanding Gondwana Input]]
- [[Keyboard Input Quick Start]]
- [[Gamepad Input Quick Start]]
- [[Touch Input Quick Start]]
- [[Using Views and Cameras]]
