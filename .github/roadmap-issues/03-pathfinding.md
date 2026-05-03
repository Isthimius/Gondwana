---
title: "feat: Built-in A* pathfinding with PathFollowMovementScript"
---
## Summary
Gondwana has no pathfinding system. Both FlatRedBall (node network from Tiled) and GameMaker (`mp_grid`, `path_find`) provide built-in pathfinding. This issue tracks adding a `NavigationGrid` and A* implementation that integrates with the existing scene and movement layers.

## Scope of Work

### `Gondwana.Movement.NavigationGrid`
- Wraps a `SceneLayer`'s tile grid (or an explicit `bool[,]` walkability map)
- Exposes `IReadOnlyList<WorldPoint> FindPath(WorldPoint start, WorldPoint end)`
- A* with Manhattan / diagonal cost options (configurable)
- Supports dynamic walkability updates at runtime (for moving obstacles)
- Optional: path-smoothing post-process (string-pull / funnel algorithm)

### `Gondwana.Movement.Scripted.PathFollowMovementScript`
- Consumes a `NavigationGrid` path and drives a `Sprite` along it using existing `ScriptedMovement` infrastructure
- Events: `PathCompleted`, `WaypointReached`
- Properties: speed, smooth-turn radius, loop mode

## Acceptance Criteria
- [ ] `NavigationGrid.FindPath()` returns an optimal path on a simple grid map
- [ ] `PathFollowMovementScript` moves a sprite to a destination without getting stuck on tile corners
- [ ] Dynamic walkability changes (blocking/unblocking cells at runtime) are respected on the next `FindPath` call
- [ ] Integrates with existing `MovementController` / `ScriptedMovement` design without breaking the API
- [ ] Demo or test showing an AI entity following a calculated path

## Key Files / References
- `Gondwana/Movement/Scripted/ScriptedMovement.cs`
- `Gondwana/Movement/MovementController.cs`
- `Gondwana/Scenes/SceneLayer.cs` (tile grid backing store)
- FlatRedBall pathfinding: https://docs.flatredball.com/flatredball/ai/pathfinding
