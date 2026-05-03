---
title: "feat: Built-in PlatformerController with gravity, jump, and coyote-time"
---
## Summary
Gondwana ships `MovementController` with follow/scripted/integrated modes and AABB collision, but has no ready-made platformer physics. FlatRedBall ships production-ready platformer movement out of the box. This issue tracks adding a `PlatformerController` on top of existing primitives.

## Scope of Work
Add `Gondwana.Movement.PlatformerController` that wraps `MovementController.Integrated` and provides:

- **Gravity** — configurable `float GravityAcceleration` and `float MaxFallSpeed`
- **Ground detection** — via `CollisionResolver` AABB bottom-edge test
- **Jump** — `Jump()` method with configurable peak height and apex time (computes initial velocity automatically)
- **Coyote time** (`float CoyoteTimeSec = 0.1f`) — grace window for jumping after walking off a ledge
- **Jump buffering** (`float JumpBufferSec = 0.083f`) — queued jump input processed on next landing
- **Wall-slide** — optional, with configurable friction coefficient
- **Horizontal deceleration** — ground friction vs. air drag curve

Integration requirements:
- Works with `CollisionGroupRegistry` and `ICollisionMovableEntity`
- Additive — does not replace `MovementController`; wraps it
- Physics parameters are data-driven and tweakable at runtime
- Demonstrate in a new `Demos/Platformer` project

## Acceptance Criteria
- [ ] Player falls with gravity and lands on solid tiles/sprites
- [ ] Jump reaches a predictable arc height with configurable peak/apex parameters
- [ ] Coyote time and jump buffering work correctly and independently
- [ ] A `Demos/Platformer` project compiles and runs on both WinForms and Avalonia

## Key Files / References
- `Gondwana/Movement/MovementController.Integrated.cs`
- `Gondwana/Collisions/CollisionResolver.cs`
- `Gondwana/Collisions/Aabb.cs`
- FlatRedBall platformer: https://docs.flatredball.com/flatredball/tutorials/platformer
