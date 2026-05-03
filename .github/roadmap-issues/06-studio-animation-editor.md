---
title: "feat: Animation Editor panel in Gondwana.Studio"
---
## Summary
There is no visual animation editor in Gondwana.Studio. FlatRedBall Glue ships a full animation editor for defining frame sequences with timing. This issue tracks adding an **Animation Editor** dockable panel to Gondwana.Studio.

## Scope of Work
Add an `AnimationEditorView` (Avalonia UserControl) that:
- Loads a `.gondwana-tilesheet` file as its source (tile picker grid on the left)
- Allows drag-drop ordering of tile thumbnails into a **FrameSequence** list (right panel)
- Per-frame duration editor (milliseconds, inline)
- Live playback preview at configured FPS (press ▶ to preview, ■ to stop)
- `CycleType` selector: Once / Loop / PingPong (maps to existing `CycleType` enum)
- Exports named animation assets as `.gondwana-animation` JSON

### `.gondwana-animation` File Format
```json
{
  "tilesheetPath": "sprites.gondwana-tilesheet",
  "name": "walk_right",
  "cycleType": "Loop",
  "frames": [
    { "tileIndex": 0, "durationMs": 100 },
    { "tileIndex": 1, "durationMs": 100 },
    { "tileIndex": 2, "durationMs": 100 }
  ]
}
```

### Engine Integration
The exported format maps 1:1 to:
- `Gondwana/Drawing/Animation/FrameSequence.cs`
- `Gondwana/Drawing/Animation/CycleType.cs`

No new engine code is required — loading is purely deserialization.

## Acceptance Criteria
- [ ] Tile picker loads tiles from a `.gondwana-tilesheet` file
- [ ] Frames can be added, reordered, and their durations edited inline
- [ ] Live preview plays the animation correctly at the configured FPS
- [ ] Exported `.gondwana-animation` deserializes into a working `FrameSequence` in the engine
- [ ] Opening the editor from the directory panel double-click works on existing `.gondwana-animation` files

## Key Files / References
- `Gondwana/Drawing/Animation/FrameSequence.cs`
- `Gondwana/Drawing/Animation/Cycle.cs`
- `Gondwana/Drawing/Animation/CycleType.cs`
- `Gondwana/Drawing/Animation/Animator.cs`
- `Tooling/Gondwana.Studio/Views/`
