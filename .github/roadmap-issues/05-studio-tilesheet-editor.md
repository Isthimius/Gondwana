---
title: "feat: Tilesheet Editor panel in Gondwana.Studio"
---
## Summary
Gondwana.Studio currently has directory and asset-file views but no visual sprite/tilesheet editor. FlatRedBall Glue and GameMaker both have integrated tilesheet/animation editors. This issue tracks adding a **Tilesheet Editor** dockable panel to Gondwana.Studio.

## Current State
The IDE lives at `Tooling/Gondwana.Studio/`. It uses Avalonia with dockable windows (`Docking/`) and already has asset file panels (`Views/AssetFilesView.axaml`).

## Scope of Work
Add a `TilesheetEditorView` (Avalonia UserControl) that:
- Opens an image file (PNG, BMP) via drag-drop or file picker
- Overlays a configurable tile grid (tile width × tile height, with live pixel preview)
- Allows naming individual tiles or ranges by clicking/selecting cells
- Exports a `.gondwana-tilesheet` JSON metadata file

### `.gondwana-tilesheet` File Format
```json
{
  "imagePath": "relative/path.png",
  "tileWidth": 16,
  "tileHeight": 16,
  "tiles": [
    { "index": 0, "name": "grass" },
    { "index": 1, "name": "dirt" }
  ]
}
```
This must be deserializable by `TilesheetRegistry` at runtime.

### Integration
- Register as a dockable panel in `MainWindow.axaml`
- Add **File → New → Tilesheet** menu entry
- Open existing `.gondwana-tilesheet` files from the directory panel double-click

## Acceptance Criteria
- [ ] User can open a PNG, set tile dimensions, and see the grid overlay immediately
- [ ] Clicking a tile cell opens an inline name-editor for that tile
- [ ] Saving exports a valid `.gondwana-tilesheet` JSON
- [ ] Runtime `TilesheetRegistry` can load the exported file and render tiles correctly

## Key Files / References
- `Tooling/Gondwana.Studio/Views/`
- `Tooling/Gondwana.Studio/Docking/`
- `Gondwana/Drawing/Tilesheets/Tilesheet.cs`
- `Gondwana/Drawing/Tilesheets/TilesheetRegistry.cs`
