> A compact, code-first reference for wiring keyboard input into a Gondwana game.

---

## Contents

- [The Three-Step Pattern](#the-three-step-pattern)
- [Show the A Key Being Pressed](#show-the-a-key-being-pressed)
- [Pressed, Repeated, and Released](#pressed-repeated-and-released)
- [Arrow-Key Movement](#arrow-key-movement)
- [Modifier Keys](#modifier-keys)
- [Held-Key Movement](#held-key-movement)
- [Pause or Remove a Binding](#pause-or-remove-a-binding)
- [Platform Key Codes](#platform-key-codes)
- [Cleanup](#cleanup)
- [Cheat Sheet](#cheat-sheet)
- [Common Problems](#common-problems)
- [Further Reading](#further-reading)

---

# The Three-Step Pattern

Keyboard input in Gondwana follows three steps:

```text
Subscribe
    ↓
Register the keys to monitor
    ↓
Handle KeyAction
```

In a `GameHostBase`-derived host, wire it up after the platform adapter is ready:

```csharp
protected override void OnKeyboardAdapterInitialized()
{
    var keyboard = Engine.Input.KeyboardEventPoller;
    if (keyboard is null)
        return;

    keyboard.KeyDown += OnKeyDown;

    keyboard.StartMonitoringKey(
        (int)Keys.A,
        nameof(Keys.A));
}
```

> The examples on this page use WinForms `Keys`. Avalonia and Blazor use their own key enums, but the Gondwana event pattern is the same.

---

# Show the A Key Being Pressed

```csharp
using Gondwana.Input.Keyboard;
using System.Windows.Forms;
```

```csharp
protected override void OnKeyboardAdapterInitialized()
{
    var keyboard = Engine.Input.KeyboardEventPoller;
    if (keyboard is null)
        return;

    keyboard.KeyDown += OnKeyDown;

    keyboard.StartMonitoringKey(
        (int)Keys.A,
        nameof(Keys.A));
}

private void OnKeyDown(KeyDownEventArgs e)
{
    if (e.KeyConfig.Key == nameof(Keys.A) &&
        e.KeyAction == KeyAction.Pressed)
    {
        Console.WriteLine("A was pressed.");
    }
}
```

`Pressed` occurs once when the key transitions from up to down.

---

# Pressed, Repeated, and Released

```csharp
private void OnKeyDown(KeyDownEventArgs e)
{
    if (e.KeyConfig.Key != nameof(Keys.A))
        return;

    switch (e.KeyAction)
    {
        case KeyAction.Pressed:
            Console.WriteLine("A started.");
            break;

        case KeyAction.Repeated:
            Console.WriteLine("A is still held.");
            break;

        case KeyAction.Released:
            Console.WriteLine("A stopped.");
            break;
    }
}
```

| Action | When it occurs |
|---|---|
| `Pressed` | Once when the key goes down |
| `Repeated` | While the key remains held, subject to throttling |
| `Released` | Once when the key comes up |

---

# Arrow-Key Movement

Register the keys:

```csharp
protected override void OnKeyboardAdapterInitialized()
{
    var keyboard = Engine.Input.KeyboardEventPoller;
    if (keyboard is null)
        return;

    keyboard.KeyDown += OnKeyDown;

    keyboard.StartMonitoringKey(
        (int)Keys.Left,
        nameof(Keys.Left));

    keyboard.StartMonitoringKey(
        (int)Keys.Right,
        nameof(Keys.Right));

    keyboard.StartMonitoringKey(
        (int)Keys.Up,
        nameof(Keys.Up));

    keyboard.StartMonitoringKey(
        (int)Keys.Down,
        nameof(Keys.Down));
}
```

Respond once per press:

```csharp
private void OnKeyDown(KeyDownEventArgs e)
{
    if (e.KeyAction != KeyAction.Pressed)
        return;

    switch (e.KeyConfig.Key)
    {
        case nameof(Keys.Left):
            MovePlayer(-1, 0);
            break;

        case nameof(Keys.Right):
            MovePlayer(1, 0);
            break;

        case nameof(Keys.Up):
            MovePlayer(0, -1);
            break;

        case nameof(Keys.Down):
            MovePlayer(0, 1);
            break;
    }
}
```

This pattern is appropriate for grid movement, menu navigation, and one-step actions.

---

# Modifier Keys

`KeyDownEventArgs` provides convenience properties:

```csharp
e.IsShift
e.IsCtrl
e.IsAlt
```

Example: Ctrl+S.

```csharp
keyboard.StartMonitoringKey(
    (int)Keys.S,
    nameof(Keys.S));
```

```csharp
private void OnKeyDown(KeyDownEventArgs e)
{
    if (e.KeyAction == KeyAction.Pressed &&
        e.KeyConfig.Key == nameof(Keys.S) &&
        e.IsCtrl)
    {
        SaveGame();
    }
}
```

Multiple modifiers may be active at once.

```csharp
if (e.IsCtrl && e.IsShift)
{
    // Ctrl+Shift+key
}
```

---

# Held-Key Movement

Use `Pressed` and `Released` to maintain game state:

```csharp
private bool _moveLeft;
```

```csharp
private void OnKeyDown(KeyDownEventArgs e)
{
    if (e.KeyConfig.Key != nameof(Keys.Left))
        return;

    switch (e.KeyAction)
    {
        case KeyAction.Pressed:
            _moveLeft = true;
            break;

        case KeyAction.Released:
            _moveLeft = false;
            break;
    }
}
```

Then apply movement in your update logic:

```csharp
if (_moveLeft)
{
    player.Movement.SetVelocity(
        new Vector2(-3f, 0f));
}
```

This is usually better than tying physics directly to keyboard-repeat timing.

---

# Pause or Remove a Binding

Pause all keyboard events:

```csharp
Engine.Input.KeyboardEventPoller!
    .PauseAllKeyEvents = true;
```

Resume:

```csharp
Engine.Input.KeyboardEventPoller!
    .PauseAllKeyEvents = false;
```

Stop monitoring one key:

```csharp
keyboard.StopMonitoringKey(
    (int)Keys.A);
```

Stop monitoring every key:

```csharp
keyboard.StopMonitoringAllKeys();
```

For complete temporary suppression, prefer the global pause or stop monitoring. A per-key configuration pause is primarily relevant to held-key repeat behavior in the current implementation.

---

# Platform Key Codes

The Gondwana core accepts integer key codes. Use the enum supplied by the active platform adapter.

## WinForms

```csharp
using System.Windows.Forms;

keyboard.StartMonitoringKey(
    (int)Keys.Space,
    nameof(Keys.Space));
```

## Avalonia

```csharp
using Avalonia.Input;

keyboard.StartMonitoringKey(
    (int)Key.Space,
    nameof(Key.Space));
```

## Blazor

```csharp
keyboard.StartMonitoringKey(
    (int)BlazorKey.Space,
    nameof(BlazorKey.Space));
```

Do not mix codes from one platform enum with another platform's adapter.

---

# Cleanup

Detach the handler when the host is disposed:

```csharp
protected override void UnhookEvents()
{
    if (Engine.Input.KeyboardEventPoller is { } keyboard)
        keyboard.KeyDown -= OnKeyDown;
}
```

`GameHostBase.Dispose()` calls `UnhookEvents()` before the engine is torn down.

---

# Cheat Sheet

## Register a key

```csharp
keyboard.StartMonitoringKey(
    (int)Keys.A,
    nameof(Keys.A));
```

## One-time press

```csharp
if (e.KeyAction == KeyAction.Pressed)
{
}
```

## Held key

```csharp
if (e.KeyAction == KeyAction.Repeated)
{
}
```

## Release

```csharp
if (e.KeyAction == KeyAction.Released)
{
}
```

## Ctrl+key

```csharp
if (e.IsCtrl)
{
}
```

## Pause everything

```csharp
keyboard.PauseAllKeyEvents = true;
```

---

# Common Problems

## Nothing happens

Check that:

- the platform adapter was initialized
- `KeyboardEventPoller` is not `null`
- the key was registered
- the engine is running
- the key code belongs to the active platform

## `KeyConfig.Key` is a number

Supply a display name:

```csharp
keyboard.StartMonitoringKey(
    (int)Keys.A,
    nameof(Keys.A));
```

## The action repeats while held

Filter for:

```csharp
e.KeyAction == KeyAction.Pressed
```

## Arrow keys are swallowed by a WinForms control

The WinForms adapter uses a global message filter and is designed to see arrow-key state regardless of normal control-key handling. Confirm the adapter was initialized against a live control and has not been disposed.

---

# Further Reading

- [[Understanding Gondwana Input]]
- [[Mouse Input Quick Start]]
- [[Gamepad Input Quick Start]]
- [[Touch Input Quick Start]]
- [[Make Your First Game in 30 Minutes]]
