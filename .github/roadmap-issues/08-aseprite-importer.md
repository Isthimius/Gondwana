---
title: "feat: Aseprite (.aseprite) importer for sprites and animations"
---
## Summary
FlatRedBall has native drag-and-drop `.aseprite` support. Gondwana has no importer for this format. Aseprite is the most popular pixel-art editor in 2D game development. This issue tracks adding a `Gondwana.Assets.Aseprite` package.

## Scope of Work

### New NuGet Package: `Gondwana.Assets.Aseprite`
- Parse the `.aseprite` binary format (spec link below)
- Extract individual frames as composited bitmaps (respect layer blend modes and visibility)
- Extract named tags → `FrameSequence` objects
- Output a `TilesheetMemory` (in-memory tilesheet) + `IDictionary<string, FrameSequence>` keyed by tag name

### Public API
```csharp
var imported = AsepriteImporter.Load("hero.aseprite");
Tilesheet tilesheet = imported.Tilesheet;
FrameSequence walkRight = imported.Animations["walk_right"];
FrameSequence idle = imported.Animations["idle"];

// Optional: save to disk
imported.ExportTilesheet("hero.gondwana-tilesheet");
imported.ExportAnimations("hero/");
```

### Notes
- The package must be standalone (no Gondwana.Studio dependency)
- Layer compositing uses Aseprite's blend modes (Normal, Multiply, Screen, etc.)
- Only RGB and RGBA colour modes need to be supported in v1 (Indexed mode is optional)

## Acceptance Criteria
- [ ] Loads an `.aseprite` file with multiple named tags and at least 2 layers
- [ ] Produced tilesheet frames render correctly in the engine (no visual artifacts)
- [ ] Tag-to-`FrameSequence` mapping matches Aseprite tag frame boundaries exactly
- [ ] Works with both flat (single layer) and multi-layer Aseprite files
- [ ] Package has no transitive dependencies beyond Gondwana core + SkiaSharp

## Key Files / References
- Aseprite file spec: https://github.com/aseprite/aseprite/blob/main/docs/ase-file-specs.md
- `Gondwana/Drawing/Tilesheets/Tilesheet.cs`
- `Gondwana/Drawing/Animation/FrameSequence.cs`
- Existing asset package for reference: `Tooling/Gondwana.Assets.WinForms/`
