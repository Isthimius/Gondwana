---
title: "feat: In-game UI / HUD layer with core widgets (Label, Button, ProgressBar, Panel)"
---
## Summary
Gondwana has no in-game UI widget layer. FlatRedBall ships FlatRedBall.Forms (MVVM WPF-style). GameMaker 2024+ has Flex Panels for responsive UI. This issue tracks a minimal, additive HUD/UI system layered on top of the existing renderer.

## Design Principles
- Code-first API consistent with Gondwana's overall philosophy
- `HudLayer` sits above all `SceneLayer`s in the `View` compositor (rendered last, screen-space)
- Input events wired from the existing `IMouseInput` polling
- v1: direct property manipulation — no MVVM binding required

## Scope of Work

### Package / Namespace: `Gondwana.UI`
| File | Purpose |
|---|---|
| `HudLayer.cs` | Registers with `ViewRenderer`, hosts and draws widgets |
| `Widget.cs` | Base class: `Position`, `Size`, `Visible`, `ZOrder`, `Parent` |
| `Label.cs` | Text display, backed by existing `FontManager` |
| `Button.cs` | `Label` + hit-test + `Clicked` event |
| `Panel.cs` | Rectangular container; background colour or image |
| `ProgressBar.cs` | `Value` (0–1), foreground/background colours, horizontal/vertical |
| `StackPanel.cs` | Simple vertical/horizontal auto-layout container |

All widgets render via SkiaSharp `SKCanvas`.

### Input Wiring
```csharp
// In Engine cycle, HudLayer checks mouse state and dispatches events:
if (mouseInput.IsLeftButtonJustReleased() && widget.HitTest(mousePos))
    widget.RaiseClicked();
```

## Acceptance Criteria
- [ ] A `Label` renders text at a screen-space position with correct font and colour
- [ ] A `Button` fires `Clicked` when left mouse button is released inside its bounds
- [ ] A `ProgressBar` fills proportionally to its `Value` property (0 = empty, 1 = full)
- [ ] `StackPanel` correctly spaces children vertically and horizontally
- [ ] The Spot demo can display a simple HUD (player turn indicator + a label) without visual regression

## Key Files / References
- `Gondwana/Input/Mouse/`
- `Gondwana/Drawing/Direct/TextBlock.cs`
- `Gondwana/Assets/` (FontManager)
- FlatRedBall.Forms docs: https://docs.flatredball.com/flatredball/gui/forms
