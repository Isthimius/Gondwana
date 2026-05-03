---
title: "feat: Scrolling platformer demo — Demos/PlatformerDemo"
---
## Summary
A scrolling platformer demo will showcase `PlatformerController`, `NavigationGrid` pathfinding, and the in-game UI / HUD together in a cohesive, runnable example. FlatRedBall's primary target genre is the platformer; Gondwana needs a reference project for the same.

## Dependencies
- PlatformerController (feat: Built-in PlatformerController) must be merged
- Tilemap / `.tmx` support must be merged
- Pathfinding (feat: Built-in A* pathfinding) must be merged
- In-game UI / HUD layer must be merged

## Scope of Work

### New Project: `Demos/PlatformerDemo/` (Avalonia preferred)
| Component | Details |
|---|---|
| **Level** | Side-scrolling level loaded from `Assets/level.tmx` with platforms, pits, and collectibles |
| **Player** | WASD / arrow keys; `PlatformerController` with jump, coyote-time, double-jump |
| **Enemy** | A patrolling enemy that uses `NavigationGrid` A* to walk along a tilemap path; reverses at level boundaries |
| **Collectibles** | Coin sprites; picking up a coin fires `ToastManager.Show("Coin +1!")` |
| **HUD** | Coin counter (`Label`) and health bar (`ProgressBar`) via the UI layer |
| **Camera** | `MovementController.Follow` with a lead-ahead offset (camera leads the player in the movement direction) |

### Sample Map: `Assets/level.tmx`
- Multi-layer: parallax sky background, solid platform layer, foreground decoration layer
- Object layer: `player_spawn`, `enemy_spawn x3`, `coin x10`, `exit`
- Freely licensed tileset committed with the project

## Acceptance Criteria
- [ ] Player jumps and lands on platforms with correct physics (no tunnelling at normal speeds)
- [ ] Enemy follows a calculated tile path and reverses at boundaries
- [ ] HUD updates in real time (coin counter increments, health bar reflects damage)
- [ ] Level can be modified in Tiled without touching C# code
- [ ] `dotnet run` works on all three desktop platforms

## Key Files / References
- PlatformerController (see feat issue)
- NavigationGrid (see pathfinding issue)
- `Gondwana.UI` (HUD layer)
- `Gondwana/Movement/MovementController.cs`
