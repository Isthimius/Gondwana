> A compact reference for raw touch contacts, taps, swipes, and pinch gestures in Gondwana.

---

## Contents

- [Platform Support](#platform-support)
- [The Basic Pattern](#the-basic-pattern)
- [Show a Touch Beginning](#show-a-touch-beginning)
- [Show a Tap](#show-a-tap)
- [Show a Swipe](#show-a-swipe)
- [Show a Pinch](#show-a-pinch)
- [Use the Unified Gesture Event](#use-the-unified-gesture-event)
- [Tune Gesture Recognition](#tune-gesture-recognition)
- [Convert Touch to World Coordinates](#convert-touch-to-world-coordinates)
- [Test Touch with a Mouse](#test-touch-with-a-mouse)
- [Pause or Stop Touch](#pause-or-stop-touch)
- [Cleanup](#cleanup)
- [Cheat Sheet](#cheat-sheet)
- [Common Problems](#common-problems)
- [Further Reading](#further-reading)

---

# Platform Support

Gondwana includes touch adapters for:

| Platform | Adapter |
|---|---|
| Avalonia | `AvaloniaTouchInputAdapter` |
| Blazor | `BlazorTouchAdapter` |
| WinForms | No built-in touch adapter |

Avalonia and Blazor game hosts initialize touch automatically.

This page uses the host hook:

```csharp
protected override void OnTouchAdapterInitialized()
```

---

# The Basic Pattern

Touch input can be consumed at two levels:

```text
Raw contacts
    TouchBegan
    TouchMoved
    TouchEnded

Recognized gestures
    Tap
    Swipe
    Pinch
```

The poller owns the built-in recognizers, so no separate recognizer construction is required.

```csharp
protected override void OnTouchAdapterInitialized()
{
    var touch = Engine.Input.TouchEventPoller;
    if (touch is null)
        return;

    touch.TouchBegan += OnTouchBegan;
    touch.TouchMoved += OnTouchMoved;
    touch.TouchEnded += OnTouchEnded;
    touch.TouchEvent += OnTouchGesture;

    touch.StartMonitoringTouch();
}
```

---

# Show a Touch Beginning

```csharp
using Gondwana.Input.Touch;
```

```csharp
private void OnTouchBegan(
    object? sender,
    TouchEventArgs e)
{
    Console.WriteLine(
        $"Touch {e.Touch.Id} began at {e.Touch.Position}.");
}
```

Track movement:

```csharp
private void OnTouchMoved(
    object? sender,
    TouchEventArgs e)
{
    Console.WriteLine(
        $"Touch {e.Touch.Id} moved to {e.Touch.Position}.");
}
```

Track normal endings and cancellations:

```csharp
private void OnTouchEnded(
    object? sender,
    TouchEventArgs e)
{
    if (e.Touch.Phase == TouchPhase.Cancelled)
    {
        CancelInteraction(e.Touch.Id);
        return;
    }

    CompleteInteraction(e.Touch.Id);
}
```

---

# Show a Tap

Use the unified gesture event:

```csharp
using Gondwana.Input.Touch.Gestures;
```

```csharp
private void OnTouchGesture(
    GestureEventArgs e)
{
    if (!e.IsTap)
        return;

    Console.WriteLine(
        $"Tap at {e.Tap!.Position}.");
}
```

Or subscribe directly to the recognizer:

```csharp
touch.TapRecognizer.Tapped +=
    OnTapped;
```

```csharp
private void OnTapped(
    object? sender,
    TappedEventArgs e)
{
    Console.WriteLine(
        $"Touch {e.TouchId} tapped at {e.Position}.");
}
```

---

# Show a Swipe

```csharp
private void OnTouchGesture(
    GestureEventArgs e)
{
    if (!e.IsSwipe)
        return;

    var swipe = e.Swipe!;

    Console.WriteLine(
        $"Swipe {swipe.Direction} " +
        $"at {swipe.SpeedPixelsPerSecond:0} px/s.");
}
```

Respond by direction:

```csharp
switch (swipe.Direction)
{
    case SwipeDirection.Left:
        PreviousPage();
        break;

    case SwipeDirection.Right:
        NextPage();
        break;

    case SwipeDirection.Up:
        OpenInventory();
        break;

    case SwipeDirection.Down:
        CloseInventory();
        break;
}
```

---

# Show a Pinch

```csharp
private void OnTouchGesture(
    GestureEventArgs e)
{
    if (!e.IsPinch)
        return;

    var pinch = e.Pinch!;

    if (pinch.Phase != PinchPhase.Updated)
        return;

    Console.WriteLine(
        $"Scale delta: {pinch.ScaleDelta:0.000}");
}
```

Apply it to a view:

```csharp
var view =
    RenderSurface.Host.ViewManager.Views[0];

float targetZoom = Math.Clamp(
    view.Viewport.Zoom *
    (float)pinch.ScaleDelta,
    view.MinZoom,
    view.MaxZoom);

view.ZoomAroundScreenPoint(
    Scene![0],
    pinch.Center,
    targetZoom,
    durationSeconds: 0f);
```

`pinch.Center` is the midpoint between the two contacts.

Interpretation:

```text
ScaleDelta > 1.0  → fingers spread apart
ScaleDelta < 1.0  → fingers move together
```

---

# Use the Unified Gesture Event

One handler can process every gesture:

```csharp
private void OnTouchGesture(
    GestureEventArgs e)
{
    switch (e.GestureType)
    {
        case GestureType.Tap:
            HandleTap(e.Tap!);
            break;

        case GestureType.Swipe:
            HandleSwipe(e.Swipe!);
            break;

        case GestureType.Pinch:
            HandlePinch(e.Pinch!);
            break;
    }
}
```

Equivalent convenience checks:

```csharp
e.IsTap
e.IsSwipe
e.IsPinch
```

---

# Tune Gesture Recognition

## Tap

Defaults:

```text
Maximum duration: 0.3 seconds
Maximum movement: 20 pixels
```

Change them:

```csharp
touch.TapRecognizer
    .MaxTapDurationSeconds = 0.25;

touch.TapRecognizer
    .MaxTapMovementPixels = 15;
```

## Swipe

Defaults:

```text
Minimum distance: 30 pixels
Minimum speed: 200 pixels/second
```

Change them:

```csharp
touch.SwipeRecognizer
    .MinimumSwipeDistancePixels = 40;

touch.SwipeRecognizer
    .MinimumSwipeSpeedPixelsPerSecond = 250;
```

## Pinch

No threshold setup is required. Pinch begins when exactly two contacts are active.

---

# Convert Touch to World Coordinates

Touch positions are render-surface coordinates.

```csharp
private void HandleTap(
    TappedEventArgs tap)
{
    var view =
        RenderSurface.Host.ViewManager.Views[0];

    var layer = Scene![0];

    PointF worldPx =
        view.ScreenPxToWorldPx(
            layer,
            tap.Position);

    PointF grid =
        view.ScreenPxToGrid(
            layer,
            tap.Position);

    SelectAt(worldPx, grid);
}
```

With multiple views, first determine which viewport contains the touch position.

---

# Test Touch with a Mouse

Avalonia ignores mouse pointers for touch by default. This prevents duplicate mouse and touch actions.

For desktop testing, initialize the Avalonia adapter with mouse emulation:

```csharp
Engine.InitializeAvaloniaTouchAdapter(
    RenderSurface,
    emulateMouse: true);
```

A mouse-emulated contact uses:

```text
Touch ID = 0
Left mouse button = contact
```

Do not enable mouse emulation when the same left click is already handled separately as mouse input, unless both routes are intentional.

---

# Pause or Stop Touch

Pause:

```csharp
touch.Configuration!.IsPaused = true;
```

Resume:

```csharp
touch.Configuration!.IsPaused = false;
```

Stop:

```csharp
touch.StopMonitoringTouch();
```

Restart:

```csharp
touch.StartMonitoringTouch();
```

Pausing or stopping clears active contact and gesture state. A contact that began before the pause will not complete a gesture after resume.

---

# Cleanup

```csharp
protected override void UnhookEvents()
{
    if (Engine.Input.TouchEventPoller is not { } touch)
        return;

    touch.TouchBegan -= OnTouchBegan;
    touch.TouchMoved -= OnTouchMoved;
    touch.TouchEnded -= OnTouchEnded;
    touch.TouchEvent -= OnTouchGesture;
}
```

When subscribing directly to a recognizer, detach that handler too:

```csharp
touch.TapRecognizer.Tapped -= OnTapped;
```

---

# Cheat Sheet

## Raw beginning

```csharp
touch.TouchBegan += OnTouchBegan;
```

## Raw movement

```csharp
touch.TouchMoved += OnTouchMoved;
```

## Raw ending

```csharp
touch.TouchEnded += OnTouchEnded;
```

## Unified gestures

```csharp
touch.TouchEvent += OnTouchGesture;
```

## Tap

```csharp
if (e.IsTap)
{
    Point p = e.Tap!.Position;
}
```

## Swipe

```csharp
if (e.IsSwipe)
{
    SwipeDirection direction =
        e.Swipe!.Direction;
}
```

## Pinch

```csharp
if (e.IsPinch)
{
    double scale =
        e.Pinch!.ScaleDelta;
}
```

## Active contacts

```csharp
foreach (var point in touch.ActiveTouches)
{
}
```

---

# Common Problems

## `TouchEventPoller` is null

The platform touch adapter was not initialized.

Avalonia and Blazor hosts do this automatically. WinForms requires a custom `ITouchAdapter`.

## Touch works on a device but not with a desktop mouse

Enable Avalonia mouse emulation when initializing the adapter.

## One mouse click causes both mouse and touch actions

Disable `emulateMouse`, or make one subsystem own the action.

## Movement seems less frequent than native pointer events

Touch movement is throttled. Beginnings and endings are not.

Use:

```csharp
touch.StartMonitoringTouch(
    timeBetweenEvents: 0);
```

when every engine-cycle movement sample is required.

## A gesture survives a modal pause

It should not. Pausing resets contacts and recognizers. Confirm the game is pausing the `TouchEventPoller` configuration rather than only ignoring callbacks.

## Tap and swipe thresholds feel wrong at high DPI

Thresholds are expressed in client pixels. Tune them for the target device and UI scale.

## Pinch stops when a third finger lands

That is intentional. Pinch updates require exactly two active contacts and safely rebase when two remain.

---

# Further Reading

- [[Understanding Gondwana Input]]
- [[Keyboard Input Quick Start]]
- [[Mouse Input Quick Start]]
- [[Gamepad Input Quick Start]]
- [[Using Views and Cameras]]
