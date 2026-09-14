# ToastWidget

`ToastWidget` displays a temporary text notification inside a `View`. It can slide into place from a view edge or fade in, remain visible for a configured duration, and then dismiss itself.

Use it for messages such as **Game saved**, **Achievement unlocked**, **Controller connected**, or other non-blocking notifications.

> These examples assume you already have a `RenderSurfaceHostBase` named `host` and, where appropriate, a `View` named `view`, a `SceneLayer` named `sceneLayer`, or a `Sprite` named `sprite`.


## Basic anchored toast

```csharp
using System.Drawing;
using Gondwana.Widgets.Overlays;

var toast = new ToastWidget(
    host,
    view,
    new Size(320, 72),
    "Game saved",
    WidgetAnchor.TopRight)
{
    Transition = ToastTransition.Slide,
    SlideOrigin = ToastSlideOrigin.Right,
    HoldDurationSec = 2.5f
};

toast.ShowToast();
```

## Fade transition

```csharp
toast.Transition = ToastTransition.Fade;
toast.TransitionDurationSec = 0.25f;
toast.ShowToast();
```

## Persistent toast

Set `HoldDurationSec` to `null` when the toast should remain until explicitly dismissed:

```csharp
toast.HoldDurationSec = null;
toast.ShowToast();

// Later:
toast.Dismiss();
```

By default, clicking the toast also dismisses it.

```csharp
toast.DismissOnClick = false;
```

## Exact positioning

```csharp
var toast = new ToastWidget(
    host,
    view,
    new Rectangle(100, 80, 360, 72),
    "Network connection restored");
```

For slide transitions, you can also provide an exact source position:

```csharp
toast.SourceLocationPx = new Point(640, 80);
```

## Reuse

The default is `DisposeOnDismiss = true`. To reuse the same instance:

```csharp
toast.DisposeOnDismiss = false;
toast.ShowToast();

// after dismissal:
toast.SetText("Second notification");
toast.ShowToast();
```

## Useful members

| Member | Purpose |
| --- | --- |
| `Transition` | `Slide` or `Fade`. |
| `SlideOrigin` | Edge used for slide transitions. |
| `HoldDurationSec` | Time at rest; `null` disables automatic dismissal. |
| `TransitionDurationSec` | Entrance/exit duration. |
| `DismissOnClick` | Whether clicking dismisses it. |
| `AnimateDismissal` | Whether exit reverses the entrance transition. |
| `CurrentState` | Hidden, entering, holding, or exiting. |
| `Dismissed` | Raised after dismissal completes. |
| `ShowToast()` / `Dismiss()` | Starts and ends the notification. |
