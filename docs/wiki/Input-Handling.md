Gondwana reacts to input via polling, not via platform-events

Platform adapters feed raw device state into engine pollers, and the engine checks those pollers during its background task phase.

---

## Input systems
The engine exposes centralized access to:
- keyboard
- mouse
- gamepad
- touch

via `EngineInputSystems`.

---

## Engine cycle placement
Input is polled during `DoBackgroundTasks`, before rendering. That means input changes can affect movement, animation, or scene state before the next frame is drawn.

Gamepads have two cadence-limited manager operations at the start of that phase: connection discovery (`UpdateConnections()`, default `0.2 Hz`) and live controller-state polling (`Poll()`, default `60 Hz`). When due, they run before `BeforeBackgroundTasksExecute` and before `GamepadEventPoller`, so game code and button events consume the newest scheduled controller snapshot. These cadences are independent of render `TargetFPS`.

---

## Why polling fits Gondwana
Because the engine wants deterministic update flow:
- timer events
- input polling
- movement
- collisions
- camera updates
- rendering

Polling keeps those steps ordered and predictable.

---

## Platform boundary
The core engine does not want to know about WinForms, Avalonia, SDL2, or browser specifics. Those details stay in adapters. The engine only consumes normalized input state.

---

## Where to read next
- `Gondwana/EngineInputSystems.cs`
- `Gondwana/Input/Keyboard/KeyboardEventPoller.cs`
- `Gondwana/Input/Mouse/MouseEventPoller.cs`
- `Gondwana/Input/Gamepad/GamepadEventPoller.cs`
- `Gondwana/Input/Touch/TouchEventPoller.cs`
