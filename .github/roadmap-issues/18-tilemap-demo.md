---
title: "feat: Tilemap demo project — Demos/TilemapDemo"
---
## Summary
FlatRedBall ships dozens of genre-specific demos. Gondwana's current demos (Spot, CoordinateTest, ParticleTest) don't showcase tile-based level design. Adding a `TilemapDemo` validates the `.tmx` importer and gives developers a working reference project.

## Dependencies
- `.tmx` tilemap support (Tiled integration issue) must be merged first

## Scope of Work

### New Project: `Demos/TilemapDemo/` (Avalonia preferred)
- **Level loading**: load `Assets/level1.tmx` at startup via `TmxImporter`
- **Rendering**: display at least 2 tile layers with different parallax factors
- **Player**: move a sprite with WASD/arrow keys using translation (no platformer physics required — this demo is top-down)
- **Collision**: solid tiles prevent the player from moving through them (AABB vs tile grid)
- **Debug overlay**: hovering a tile displays its name (from the TMX object layer) as a `Label`
- **Camera**: follows the player with a `MovementController.Follow` binding

### Sample Map: `Assets/level1.tmx`
Must be committed to the repo with the project:
- At least 2 tile layers (background + foreground with parallax difference)
- A collision layer defining walkable vs. solid tiles
- An object layer with ≥ 3 named entities: `player_spawn`, `enemy_spawn`, `exit`
- Uses a freely licensed 16×16 tilesheet (e.g., [Kenney.nl tilemap packs](https://kenney.nl/assets))

### Verification
The level should be editable in Tiled (https://www.mapeditor.org/) and reload in-engine without any code changes.

## Acceptance Criteria
- [ ] `dotnet run` in `Demos/TilemapDemo/` starts the game with a visible, multi-layer tiled level
- [ ] Player cannot walk through solid tiles (AABB collision works)
- [ ] Parallax layers scroll at visibly different rates as the player moves
- [ ] Hovering/touching a tile shows its name from the TMX object layer

## Key Files / References
- TmxImporter (see tilemap support issue)
- `Gondwana/Movement/MovementController.cs`
- `Gondwana/Collisions/CollisionResolver.cs`
- Kenney tile assets (public domain): https://kenney.nl/assets/tiny-town
