---
title: "feat: Basic 2D lighting system (point lights, darkness layer, additive blend)"
---
## Summary
Neither FlatRedBall nor GameMaker ships a deeply sophisticated 2D lighting engine, but both support sprite tinting, additive blending, and simple composited light sources. Gondwana has no lighting layer at all. This issue tracks adding a lightweight layered 2D lighting system.

## Approach: Darkness-Layer Compositing
1. Render the scene normally to the main backbuffer
2. Fill a separate **darkness layer** (solid black with configurable alpha = ambient darkness)
3. For each `LightSource`, punch a radial-gradient "hole" in the darkness layer using multiply blend
4. Composite the darkness layer over the scene using multiply blend

This avoids shadow geometry entirely and is fast enough for many 2D games.

## Scope of Work

### `Gondwana.Drawing.LightSource`
```csharp
public class LightSource : IDirectDrawable
{
    public LightType Type { get; set; }   // Point | Directional | Ambient
    public SKColor Color { get; set; }
    public float Intensity { get; set; } // 0–1
    public float Radius { get; set; }    // world units (point lights)
    public float SoftEdgeFalloff { get; set; }
}
```

### `LightingLayer` (scene layer)
- Aggregates all `LightSource` instances registered in a `Scene`
- Renders the darkness surface via SkiaSharp radial gradient `SKShader` + multiply `SKBlendMode`
- `Scene.AmbientLight` property (0.0 = pitch black, 1.0 = fully lit, no darkness layer drawn)

## Acceptance Criteria
- [ ] A single point light produces a visible lit circle with soft edges
- [ ] Multiple lights composite correctly without blending artefacts
- [ ] `AmbientLight = 1.0f` is a zero-cost no-op (darkness layer not rendered)
- [ ] Works with both BitmapBackbuffer and GpuBackbuffer
- [ ] No visible frame-rate regression with up to 16 light sources in a scene

## Key Files / References
- `Gondwana/Scenes/SceneLayer.cs`
- `Gondwana/Drawing/Direct/DirectDrawingManager.cs`
- `Gondwana/Rendering/Backbuffers/` (both backbuffer implementations)
