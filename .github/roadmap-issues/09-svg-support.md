---
title: "feat: SVG asset support (SvgResource backed by SkiaSharp)"
---
## Summary
GameMaker 2024 added SVG support for sharp UI artwork at any resolution. Gondwana already uses SkiaSharp, which exposes SVG rendering via `SkiaSharp.Extended.Svg` or `Svg.Skia`. This issue tracks adding a `SvgResource` type to the asset pipeline.

## Scope of Work

### `Gondwana.Drawing.SvgResource`
```csharp
public class SvgResource : IDisposable
{
    public static SvgResource Load(string path);

    // Rasterize at an explicit size
    public SKBitmap Rasterize(int width, int height);

    // Rasterize at the SVG's intrinsic size scaled by a factor
    public SKBitmap Rasterize(float scale = 1.0f);

    public SizeF IntrinsicSize { get; }
}
```

- Register with `ResourceManager` (add `SvgResource` to the asset loading pipeline)
- Cache rasterized bitmaps; invalidate cache only when `scale` or `size` changes

### `DirectSvg` Drawable
A `IDirectDrawable` implementation that wraps `SvgResource`:
- Rasterizes lazily on first draw
- Re-rasterizes if the render scale changes (e.g., window resize on high-DPI)
- Otherwise re-uses the cached `SKBitmap` with zero overhead per frame

## Notes
- SVG is most useful for UI icons, HUD elements, and scalable logos — not large game-world tile art
- Rasterization is expensive; never rasterize in a draw callback without caching

## Acceptance Criteria
- [ ] An SVG file loads and renders at two different sizes producing correctly scaled, sharp bitmaps
- [ ] `DirectSvg` renders in the engine without artefacts or per-frame rasterization
- [ ] Cache is only invalidated when the requested size actually changes
- [ ] Works in both Bitmap and GPU backbuffer paths

## Key Files / References
- `Gondwana/Drawing/Direct/DirectImage.cs`
- `Gondwana/Assets/` (ResourceManager)
- SkiaSharp SVG libraries: https://github.com/mono/SkiaSharp/wiki/SkiaSharp.Extended
