---
title: "feat: Tilemap / .tmx support (Tiled integration)"
---
## Summary
Gondwana currently has no way to load levels authored in [Tiled](https://www.mapeditor.org/) (.tmx files). Both FlatRedBall and GameMaker offer native Tiled/room-editor integration, giving developers a visual level-design workflow. This issue tracks adding a first-class `.tmx` import pipeline.

## Background
Gondwana already has `SceneLayer`, `Tile`, and `TilesheetRegistry` primitives. The missing piece is a parser that maps Tiled's XML layer structure onto these abstractions.

## Scope of Work
- Add a `TmxImporter` class (or static method) in `Gondwana.Drawing` / `Gondwana.Scenes` that:
  - Parses `.tmx` XML (tile layers → `SceneLayer`, tile instances → world-space `Tile` placements)
  - Maps Tiled object layers to engine entity-spawn lists (returns `IEnumerable<SpawnDescriptor>` so game code can instantiate `Sprite` objects)
  - Maps Tiled collision layers / object shapes to `Aabb` collision data registered with `ColliderRegistry`
  - Handles external tilesheet references (`.tsx` files) through the existing `TilesheetRegistry`
- Expose a `TmxMapResource` asset type loadable from the extensible resource pipeline
- Add a demo or unit test that loads a sample `.tmx` and verifies tile count / object placement

## Acceptance Criteria
- [ ] A `.tmx` file (Tiled 1.x format) loads into a `Scene` without manual configuration
- [ ] Tile layers render correctly with the existing dirty-region renderer
- [ ] Object-layer spawns return parseable descriptor data
- [ ] Collision data from object / tile-collision layers is accessible as `Aabb` instances
- [ ] Tested with at least one sample Tiled map committed to `Demos/`

## Key Files / References
- `Gondwana/Scenes/SceneLayer.cs`
- `Gondwana/Drawing/Tile.cs`
- `Gondwana/Drawing/Tilesheets/TilesheetRegistry.cs`
- FlatRedBall Tiled docs: https://docs.flatredball.com/tiled
- Tiled XML spec: https://doc.mapeditor.org/en/stable/reference/tmx-map-format/
