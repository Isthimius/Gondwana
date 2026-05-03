---
title: "feat: Shader / post-process effect pipeline (SKRuntimeEffect / SkSL)"
---
## Summary
FlatRedBall supports post-process shaders (bloom, CRT, custom). GameMaker ships GLSL ES. Gondwana uses SkiaSharp / Skia, which exposes `SKRuntimeEffect` (SkSL shader language) for per-pixel and full-screen effects. This issue tracks a first-class shader/effect API.

## Scope of Work

### Per-Sprite Color Filters
Add `ShaderEffect` abstract base class wrapping `SKColorFilter` or `SKImageFilter`, and apply via `Sprite.ShaderEffect` (null = no effect).

Built-in implementations:
| Class | Description |
|---|---|
| `GrayscaleEffect` | Full desaturation |
| `TintEffect(SKColor color, float strength)` | Colour tint overlay |
| `ChromaticAberrationEffect(float strength)` | RGB channel offset |
| `OutlineEffect(SKColor color, float thickness)` | Sprite outline |

### Full-Screen Post-Process Passes (GPU Backbuffer)
Add a `PostProcessPass` list to `GpuBackbuffer`, applied after scene composition.

Built-in passes:
| Class | Description |
|---|---|
| `BloomPass` | Brighten over-exposed areas |
| `CrtScanlinePass` | Classic CRT scanlines |
| `VignettePass` | Darken screen edges |

Custom: implement `IPostProcessPass.Apply(SKSurface input, SKSurface output)`.

### SkSL Custom Shaders
For advanced users: `SkslShaderEffect` compiles a user-supplied SkSL string and binds named uniform parameters:
```csharp
var effect = new SkslShaderEffect("""
    uniform float time;
    half4 main(float2 fragCoord) {
        return half4(sin(time + fragCoord.x * 0.01), 0, 0, 1);
    }
""");
effect.SetUniform("time", elapsedSec);
```

## Acceptance Criteria
- [ ] `TintEffect` correctly tints a single sprite without affecting others in the same frame
- [ ] `BloomPass` visibly brightens over-exposed areas in the GPU backbuffer path
- [ ] A custom `SkslShaderEffect` with a simple SkSL program compiles and renders without crashing
- [ ] No regression in the existing Spot demo render output

## Key Files / References
- `Gondwana/Rendering/Backbuffers/GpuBackbuffer.cs`
- `Gondwana/Drawing/Sprites/Sprite.cs`
- SkiaSharp SKRuntimeEffect: https://learn.microsoft.com/en-us/dotnet/api/skiasharp.skruntimeeffect
- SkSL language reference: https://skia.org/docs/user/sksl/
