---
title: "feat: Touch / gesture input adapter for Android and iOS (Avalonia hosting)"
---
## Summary
Gondwana targets Android and iOS via the Avalonia platform adapter, but `EngineInputSystems` has no touch abstraction. FlatRedBall and GameMaker both provide touch input primitives. This issue tracks adding a `TouchInputAdapter` to the existing input polling infrastructure.

## Scope of Work

### `Gondwana.Input.ITouchInput` Interface
```csharp
public interface ITouchInput
{
    IReadOnlyList<TouchPoint> ActiveTouches { get; }
    event EventHandler<TouchEventArgs> TouchBegan;
    event EventHandler<TouchEventArgs> TouchMoved;
    event EventHandler<TouchEventArgs> TouchEnded;
}

public readonly record struct TouchPoint(int Id, WorldPoint Position, TouchPhase Phase);
public enum TouchPhase { Began, Moved, Stationary, Ended, Cancelled }
```

### Gesture Recognizers (optional, but recommended for v1)
| Class | Description |
|---|---|
| `TapGestureRecognizer` | Single-finger tap with configurable time threshold |
| `SwipeGestureRecognizer` | Direction + minimum speed threshold |
| `PinchGestureRecognizer` | Two-finger scale delta |

### `AvaloniaTouchInputAdapter`
- Subscribes to Avalonia `PointerPressed` / `PointerMoved` / `PointerReleased` on touch devices
- Maps Avalonia pointer IDs to `TouchPoint.Id` values
- Falls back to mouse emulation on desktop (single touch point = mouse position)

### Engine Wiring
Register in `EngineInputSystems` similarly to `IKeyboardInput` and `IMouseInput`:
```csharp
engine.InputSystems.Touch = new AvaloniaTouchInputAdapter(surface);
```

## Acceptance Criteria
- [ ] On a touch device (or Android emulator), `ActiveTouches` populates correctly on finger contact
- [ ] `TapGestureRecognizer` fires `Tapped` for short single taps and ignores long presses
- [ ] Desktop mouse events are unaffected (no regression)
- [ ] Can be wired in the Avalonia hosting project without changes to the core `Gondwana` library

## Key Files / References
- `Gondwana/Input/` (Keyboard, Mouse subdirectories for reference)
- `Gondwana.Avalonia/` hosting project
- `Gondwana/Engine.cs` (EngineInputSystems wiring)
