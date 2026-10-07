> A compact reference for controller buttons, analog sticks, triggers, XInput, and SDL2 in Gondwana.

---

## Contents

- [Choose a Backend](#choose-a-backend)
- [The Basic Pattern](#the-basic-pattern)
- [Show the A Button Being Pressed](#show-the-a-button-being-pressed)
- [Register Every Connected Controller](#register-every-connected-controller)
- [Read an Analog Stick](#read-an-analog-stick)
- [Read Triggers](#read-triggers)
- [Handle Held-Button Repeat](#handle-held-button-repeat)
- [Support Hot-Plugged Controllers](#support-hot-plugged-controllers)
- [Pause or Remove Bindings](#pause-or-remove-bindings)
- [Cleanup](#cleanup)
- [Cheat Sheet](#cheat-sheet)
- [Common Problems](#common-problems)
- [Further Reading](#further-reading)

---

# Choose a Backend

Gondwana currently provides two common gamepad backends.

| Backend | Best fit | Initialization |
|---|---|---|
| XInput | Xbox-compatible controllers on Windows | `Engine.InitializeXInputGamepadManager()` |
| SDL2 | Cross-platform controller support | `Engine.InitializeSdlGamepadManager()` |

A WinForms game host initializes XInput by default.

Avalonia and Blazor hosts leave gamepad backend selection to the game.

---

# The Basic Pattern

Gamepad setup has four parts:

```text
Initialize manager
    ↓
Discover connected adapters
    ↓
Register a button for each GamepadId
    ↓
Handle ButtonDown
```

The manager owns devices. The engine schedules connection discovery and live controller-state polling independently, and the event poller owns registered button events.

By default:

- `UpdateConnections()` runs at `0.2 Hz` (once every five seconds).
- `Poll()` runs at `60 Hz`.
- both operations run in the input/simulation phase, before game callbacks and `GamepadEventPoller` consume controller state.

These cadences are controlled by `Engine.Configuration.GamepadConnectionUpdateFrequencyHz` and `Engine.Configuration.GamepadPollFrequencyHz`. They are independent of `TargetFPS`, including when rendering is uncapped.

---

# Show the A Button Being Pressed

This WinForms/XInput example registers the A button on existing controllers.

```csharp
using Gondwana.Input.Gamepad;
```

```csharp
protected override void OnGamepadManagerInitialized()
{
    var manager = Engine.Input.GamepadManager;
    var poller = Engine.Input.GamepadEventPoller;

    if (manager is null || poller is null)
        return;

    // Assigning the manager performs one initial discovery/state refresh.
    poller.ButtonDown += OnGamepadButtonDown;

    foreach (var gamepad in manager.ConnectedAdapters)
    {
        poller.StartMonitoringButton(
            gamepad.GamepadId,
            button: "A");
    }
}
```

```csharp
private void OnGamepadButtonDown(
    GamepadButtonDownEventArgs e)
{
    if (e.Config.Button == "A")
    {
        Console.WriteLine(
            $"A is down on {e.Adapter.GamepadId}.");
    }
}
```

For XInput, controller IDs look like:

```text
XInput_0
XInput_1
XInput_2
XInput_3
```

---

# Register Every Connected Controller

```csharp
private void RegisterButtons(
    IGamepadAdapter gamepad)
{
    var poller =
        Engine.Input.GamepadEventPoller!;

    poller.StartMonitoringButton(
        gamepad.GamepadId,
        "A");

    poller.StartMonitoringButton(
        gamepad.GamepadId,
        "B");

    poller.StartMonitoringButton(
        gamepad.GamepadId,
        "Start");
}
```

```csharp
foreach (var gamepad in manager.ConnectedAdapters)
{
    RegisterButtons(gamepad);
}
```

Button names come from the selected backend.

## XInput examples

```text
A
B
X
Y
Start
Back
DPadUp
DPadDown
DPadLeft
DPadRight
LeftShoulder
RightShoulder
```

## SDL2 examples

SDL2 names mirror its enum values:

```text
SDL_CONTROLLER_BUTTON_A
SDL_CONTROLLER_BUTTON_B
SDL_CONTROLLER_BUTTON_X
SDL_CONTROLLER_BUTTON_Y
```

A portable game should map backend-specific names to game actions.

---

# Read an Analog Stick

Analog sticks are continuous state. Read them from the adapter.

```csharp
private void ReadLeftStick(
    IGamepadAdapter gamepad)
{
    var stick = gamepad.LeftStick?
        .WithDeadzone(0.20f);

    if (stick is not { } value)
        return;

    if (!value.IsEngaged())
        return;

    Console.WriteLine(
        $"Stick: {value.X:0.00}, {value.Y:0.00}");
}
```

Apply it to movement:

```csharp
var direction = new Vector2(
    value.X,
    -value.Y);

player.Movement.SetVelocity(
    direction * moveSpeed);
```

The Y sign may need to be inverted because many game worlds use positive Y downward while controller APIs commonly describe up as positive.

## Direction-only input

```csharp
StickDirection direction =
    value.Direction(0.20f);

if (direction.HasFlag(
        StickDirection.Left))
{
    MoveLeft();
}
```

---

# Read Triggers

```csharp
float left =
    gamepad.LeftTrigger;

float right =
    gamepad.RightTrigger;
```

Typical values are normalized:

```text
0.0 = not pressed
1.0 = fully pressed
```

Example:

```csharp
if (gamepad.RightTrigger > 0.5f)
{
    Accelerate();
}
```

For continuous trigger behavior, read the adapter state during your update path rather than relying on a button event.

---

# Handle Held-Button Repeat

`ButtonDown` may fire repeatedly while the button remains held.

Control the interval when registering:

```csharp
poller.StartMonitoringButton(
    gamepad.GamepadId,
    "A",
    timeBetweenEvents: 0.20);
```

This is useful for menu navigation.

For a one-time action, track your own down state:

```csharp
private readonly HashSet<string>
    _activeButtons = new();
```

```csharp
private void OnGamepadButtonDown(
    GamepadButtonDownEventArgs e)
{
    string key =
        $"{e.Adapter.GamepadId}:{e.Config.Button}";

    if (!_activeButtons.Add(key))
        return;

    Jump();
}
```

To implement true press/release edge semantics for gamepad buttons, compare `PressedButtons` snapshots in a game-specific controller layer. The core `GamepadEventPoller` currently exposes held-compatible button-down events rather than a released event.

---

# Support Hot-Plugged Controllers

A controller may connect after initial setup.

Track registered IDs:

```csharp
private readonly HashSet<string>
    _registeredGamepads = new();
```

```csharp
private void RegisterNewGamepads()
{
    var manager =
        Engine.Input.GamepadManager;

    if (manager is null)
        return;

    foreach (var gamepad in manager.ConnectedAdapters)
    {
        if (!_registeredGamepads.Add(
                gamepad.GamepadId))
        {
            continue;
        }

        RegisterButtons(gamepad);
    }
}
```

Call this from an appropriate game update or periodic timer. A newly attached controller becomes visible after the next configured connection-discovery pass; the default maximum delay is about five seconds.

Do not put `UpdateConnections()` or `Poll()` in an unbounded loop. The engine schedules them independently through `GamepadConnectionUpdateFrequencyHz` and `GamepadPollFrequencyHz`.

The legacy combined `Update()` method remains for compatibility with older custom managers, but new code should use the split operations.

---

# Pause or Remove Bindings

Pause every gamepad event:

```csharp
poller.PauseAllInput = true;
```

Resume:

```csharp
poller.PauseAllInput = false;
```

Pause one configuration:

```csharp
poller
    .AllButtonConfigsByGamepadId[gamepadId]["A"]
    .IsPaused = true;
```

Stop one button:

```csharp
poller.StopMonitoringButton(
    gamepadId,
    "A");
```

Stop all buttons for one controller:

```csharp
poller.StopMonitoringAllButtons(
    gamepadId);
```

---

# Cleanup

```csharp
protected override void UnhookEvents()
{
    if (Engine.Input.GamepadEventPoller is { } poller)
        poller.ButtonDown -= OnGamepadButtonDown;
}
```

The engine also removes registered button configurations during disposal.

---

# Cheat Sheet

## Initialize XInput

```csharp
Engine.InitializeXInputGamepadManager();
```

## Initialize SDL2

```csharp
Engine.InitializeSdlGamepadManager();
```

## Register A

```csharp
poller.StartMonitoringButton(
    gamepad.GamepadId,
    "A");
```

## Handle event

```csharp
if (e.Config.Button == "A")
{
}
```

## Read left stick

```csharp
var stick =
    gamepad.LeftStick?
        .WithDeadzone(0.20f);
```

## Read triggers

```csharp
float left = gamepad.LeftTrigger;
float right = gamepad.RightTrigger;
```

## Controller identity

```csharp
string id = gamepad.GamepadId;
```

## Polling cadences

```csharp
Engine.Configuration.GamepadConnectionUpdateFrequencyHz = 0.2; // every 5 seconds
Engine.Configuration.GamepadPollFrequencyHz = 60;              // live state
```

---

# Common Problems

## No controllers appear during initialization

Assigning a gamepad manager performs one initial connection discovery and state poll, so controllers already present during initialization should appear immediately.

If a controller is attached afterward, wait for the next connection-discovery pass or deliberately request one:

```csharp
manager.UpdateConnections();
manager.Poll();
```

Ongoing automatic discovery defaults to `0.2 Hz` and state polling defaults to `60 Hz`.

## A controller appears, but A does nothing

Confirm the button name for the active backend.

XInput:

```text
A
```

SDL2:

```text
SDL_CONTROLLER_BUTTON_A
```

## One press triggers several actions

`ButtonDown` can repeat while held. Increase `timeBetweenEvents` or implement game-level edge tracking.

## The stick drifts while untouched

Apply a deadzone:

```csharp
gamepad.LeftStick?
    .WithDeadzone(0.20f);
```

## A newly connected controller has no bindings

Register the buttons for its new `GamepadId`.

## The controller state seems stale

Gondwana refreshes controller state in the input/simulation phase before game callbacks and button-event polling. The default state cadence is `60 Hz`, independent of render FPS.

If lower latency or lower polling overhead is desired, adjust:

```csharp
Engine.Configuration.GamepadPollFrequencyHz = 120;
```

Connection discovery is a separate, much slower cadence and does not control live stick/button responsiveness for already-connected controllers.

---

# Further Reading

- [[Understanding Gondwana Input]]
- [[Keyboard Input Quick Start]]
- [[Mouse Input Quick Start]]
- [[Touch Input Quick Start]]
