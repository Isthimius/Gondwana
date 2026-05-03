---
title: "feat: Scene / Room Editor panel in Gondwana.Studio"
---
## Summary
There is no visual Scene Editor in Gondwana.Studio. Developers currently hand-code all scene layout. FlatRedBall Glue and GameMaker's room editor are both central to their workflows. This issue tracks adding a **Scene Editor** dockable panel.

## Scope of Work
Add a `SceneEditorView` (Avalonia UserControl) that:
- Renders a live 2D viewport using an Avalonia canvas (or headless SkiaSharp surface) of the composed scene
- Shows a **tile palette** sourced from `.gondwana-tilesheet` files (left sidebar)
- **Stamp-paint tiles** onto a selected `SceneLayer` with configurable parallax factor
- **Drag-place** named `Sprite` entity instances at world coordinates
- **Draw axis-aligned `Aabb` collision boxes** visually (rectangle tool)
- Camera pan (middle-mouse / space+drag) and zoom (scroll wheel)
- Serialises / deserialises the scene to `.gondwana-scene` JSON

### `.gondwana-scene` File Format
```json
{
  "layers": [
    {
      "name": "background",
      "parallax": 0.5,
      "tilesheet": "tiles.gondwana-tilesheet",
      "tiles": [ { "tileIndex": 3, "x": 0, "y": 0 } ]
    }
  ],
  "entities": [
    { "name": "player_spawn", "x": 64, "y": 64 }
  ],
  "colliders": [
    { "x": 0, "y": 112, "width": 320, "height": 16 }
  ]
}
```

A runtime `SceneLoader` reads `.gondwana-scene` and constructs engine objects accordingly.

## Acceptance Criteria
- [ ] Tiles painted in the editor match the runtime rendering exactly
- [ ] Scene serialises and deserialises without data loss (round-trip test)
- [ ] Camera pan and zoom work smoothly
- [ ] The existing Spot demo level can be recreated from a `.gondwana-scene` file

## Dependencies
- Tilesheet Editor (#5) for tile palette source

## Key Files / References
- `Gondwana/Scenes/Scene.cs`
- `Gondwana/Scenes/SceneLayer.cs`
- `Gondwana/Drawing/Tilesheets/TilesheetRegistry.cs`
- `Tooling/Gondwana.Studio/Views/`
