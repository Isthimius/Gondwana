# Gondwana.Avalonia

**Gondwana.Avalonia** provides Avalonia UI adapters for rendering and input, mirroring
`Gondwana.WinForms` but targeting all platforms supported by Avalonia: **desktop** (Windows,
Linux, macOS), **WebAssembly**, **Android**, and **iOS / macOS (Catalyst)**.

## Features

- Bitmap rendering surface using Avalonia `WriteableBitmap` (no platform-specific SkiaSharp view package required; works on all Avalonia targets)
- Keyboard input integration (global key capture via Avalonia `TopLevel`)
- Mouse / pointer input integration
- Touch input integration with gesture recognizer support (`TapGestureRecognizer`, `SwipeGestureRecognizer`, `PinchGestureRecognizer`)
- Designed to be consumed by platform-specific Avalonia host projects

## Installation

```bash
dotnet add package Gondwana.Avalonia
```

## Usage

Register Avalonia adapters during application startup (inside your `Window.OnOpened` or similar):

```csharp
Engine.Instance.InitializeAvaloniaKeyboardAdapter(myWindow);
Engine.Instance.InitializeAvaloniaMouseAdapter(renderSurfaceControl);
```

### Touch input

Enable touch / gesture input by calling `InitializeAvaloniaTouchAdapter` with the control
that should receive pointer events:

```csharp
Engine.Instance.InitializeAvaloniaTouchAdapter(renderSurfaceControl);
```

After initialization, the touch system is accessible via `engine.Input.TouchEventPoller`:

```csharp
// Raw touch events
Engine.Instance.Input.TouchEventPoller!.TouchBegan += (_, e) =>
    Console.WriteLine($"Touch began: id={e.Touch.Id} pos={e.Touch.Position}");

// Active contacts (polling)
var touches = Engine.Instance.Input.TouchEventPoller!.ActiveTouches;
```

#### Gesture recognizers

Gesture recognizers are standalone classes in `Gondwana.Input.Touch.Gestures` that subscribe to
an `ITouchInput` source and raise higher-level events. Dispose them when no longer needed.

```csharp
using Gondwana.Input.Touch.Gestures;

var tap = new TapGestureRecognizer(Engine.Instance.Input.TouchEventPoller!)
{
    MaxTapDurationSeconds = 0.3,   // default
    MaxTapMovementPixels  = 20,    // default
};
tap.Tapped += (_, e) => Console.WriteLine($"Tapped at {e.Position}");

var swipe = new SwipeGestureRecognizer(Engine.Instance.Input.TouchEventPoller!)
{
    MinimumSwipeSpeedPixelsPerSecond = 200,  // default
};
swipe.Swiped += (_, e) => Console.WriteLine($"Swiped {e.Direction} at {e.SpeedPixelsPerSecond:F0} px/s");

var pinch = new PinchGestureRecognizer(Engine.Instance.Input.TouchEventPoller!);
pinch.PinchUpdated += (_, e) => Console.WriteLine($"Pinch scale delta: {e.ScaleDelta:F3}");
```

> **Desktop note:** On desktop platforms without a hardware touch screen, the mouse pointer is
> automatically emulated as a single touch contact (`Id = 0`). Existing mouse input is unaffected.

### Key codes

`AvaloniaKeyboardAdapter` uses `(int)Avalonia.Input.Key` values as key codes.  Use
`AvaloniaKeyboardAdapter.GetKeyCodeFromString("Left")` to resolve a key name at runtime.

## Documentation

-   **Source Code**\
    https://github.com/isthimius/Gondwana

-   **Architecture & Guides**\
    https://github.com/isthimius/Gondwana/wiki

-   **API Reference (Doxygen)**\
    https://isthimius.github.io/Gondwana/

## Related Packages

- `Gondwana` – Core engine
- `Gondwana.Avalonia.Hosting` – Avalonia game host

## License

MIT
