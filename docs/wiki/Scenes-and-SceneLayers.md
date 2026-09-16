A Scene is Gondwana’s top-level world container. It owns a set of SceneLayer instances, tracks global collision groups, and acts as the unit a render surface binds to when the engine draws a frame.

A SceneLayer is where most playable world content actually lives. Each layer has:
- its own tile grid
- its own coordinate system
- its own parallax factor
- its own dirty-region RefreshQueue
- its own collision registry and resolver
- its own visibility and z-order

This is one of Gondwana’s most important architectural foundations: the engine does not treat “the world” as a single flat canvas. Instead, it treats the world as a stack of independently managed layers.

---

## Why that matters
Because each SceneLayer tracks refresh state independently, Gondwana can redraw only the layers and world regions that changed. That makes layered backgrounds, gameplay layers, collision-debug overlays, and HUD-style scene content practical without forcing a full-frame redraw every cycle.

---

## Mental model
Think of a Scene as a folder, and each SceneLayer as a transparent sheet inside it:
- some layers move slower (Parallax < 1)
- some move normally (Parallax = 1)
- some can move faster (Parallax > 1)
- each can use a different projection model
- each can be hidden, reordered, shifted, or wrapped

---

## Key details
- Scene.VisibleSceneLayers is cached and sorted by ascending ZOrder
- structural layer changes mark Scene.FullRefreshNeeded = true
- layer origin is in world pixels
- tile addressing is still layer-local
- collision groups live at scene level, but layers expose them as a convenience

---

## Where to read next
- [[Tiles and Tile-Based SceneLayers]] — fixed grid cells, tiles, adjacency, wrapping, animation, and collision
- [[Rendering Order and Z-Order]] — how layers and their drawables are stacked
- [[Coordinate Systems]] — how layer grid coordinates project into world pixels
- [[Parallax and Multi-View Rendering]] — how layers move through views
- `Gondwana/Scenes/Scene.cs`
- `Gondwana/Scenes/SceneLayer.cs`
- `Gondwana/Rendering/RefreshQueue.cs`
