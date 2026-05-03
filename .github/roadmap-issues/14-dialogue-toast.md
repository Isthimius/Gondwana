---
title: "feat: DialogueBox and ToastManager for in-game text and notifications"
---
## Summary
FlatRedBall ships a `ToastManager` for transient notification overlays. Neither Gondwana nor common game-engine starting points include RPG-style dialogue boxes. This issue tracks adding `DialogueBox` and `ToastManager` built on top of the UI layer (see HUD layer issue).

## Dependencies
- In-game UI / HUD layer must be implemented first (it provides `HudLayer` and `Label`)

## Scope of Work

### `ToastManager`
```csharp
// Global singleton, registered with HudLayer
ToastManager.Show("Picked up Sword!", duration: TimeSpan.FromSeconds(2));
ToastManager.Show("Level Up!", style: ToastStyle.Success);
```
- Manages a FIFO queue of timed text notifications
- Slide-in / fade-out animation (configurable duration, easing curve)
- Configurable screen position (default: top-centre)
- Clears automatically after duration; also exposes `Dismiss()` for programmatic removal

### `DialogueBox`
```csharp
var dlg = new DialogueBox();
dlg.Show(new[] {
    new DialogueLine(speaker: "Elf",  text: "The dungeon is dangerous!"),
    new DialogueLine(speaker: "Hero", text: "I can handle it."),
});
dlg.DialogueCompleted += OnDialogueDone;
```
- Renders speaker name + body text with a configurable **typewriter effect** (characters/second)
- Advance on configurable key press or mouse click
- Second press on same line immediately reveals the full line (skip typewriter)
- Optional portrait `Sprite` slot in the dialogue frame
- Raises `DialogueCompleted` when all lines are shown and dismissed
- Raises `LineChanged` on each advance

## Acceptance Criteria
- [ ] `ToastManager.Show()` displays a timed notification that auto-dismisses after its duration
- [ ] Multiple toasts queue correctly and don't overlap
- [ ] `DialogueBox` advances through lines correctly on input key/click
- [ ] Typewriter effect can be skipped with a second press
- [ ] Both work in the Spot demo without affecting existing game logic

## Key Files / References
- Depends on: HUD layer (`Gondwana.UI`)
- `Gondwana/Input/Keyboard/`
- `Gondwana/Input/Mouse/`
- FlatRedBall ToastManager: https://docs.flatredball.com/flatredball/tutorials/toast
