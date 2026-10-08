using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using SkiaSharp;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Renders an SVG resource directly by rasterizing lazily and caching the bitmap per destination size.
/// </summary>
public sealed class DirectSvg : DirectDrawingMovableBase
{
    private readonly SvgResource _svgResource;
    private readonly SKPaint _paint = new()
    {
        IsAntialias = true,
        FilterQuality = SKFilterQuality.Medium,
        BlendMode = SKBlendMode.SrcOver
    };

    private SKBitmap? _cachedBitmap;
    private int _cachedWidth;
    private int _cachedHeight;

    private DirectSvg(SvgResource svgResource,
                      RenderSurfaceHostBase renderSurfaceHost,
                      DirectDrawingMode mode,
                      SceneLayer? sceneLayer,
                      View? view,
                      Rectangle? screenBounds,
                      Rectangle? worldBounds,
                      string? nickname = null)
        : base(renderSurfaceHost, mode, sceneLayer, view, screenBounds, worldBounds, nickname)
    {
        _svgResource = svgResource ?? throw new ArgumentNullException(nameof(svgResource));
    }

    /// <summary>
    /// Initializes a new world-space <see cref="DirectSvg"/>.
    /// </summary>
    /// <param name="svgResource">The svg resource.</param>
    /// <param name="renderSurfaceHost">The render-surface host that owns the drawing.</param>
    /// <param name="sceneLayer">The scene layer that owns the content.</param>
    /// <param name="worldBounds">The bounds in world pixels.</param>
    /// <param name="nickname">An optional name used to identify the object.</param>
    public DirectSvg(SvgResource svgResource,
                     RenderSurfaceHostBase renderSurfaceHost,
                     SceneLayer sceneLayer,
                     Rectangle worldBounds,
                     string? nickname = null)
        : this(svgResource, renderSurfaceHost, DirectDrawingMode.SceneLayer, sceneLayer, null, null, worldBounds, nickname)
    { }

    /// <summary>
    /// Initializes a new screen-space <see cref="DirectSvg"/>.
    /// </summary>
    /// <param name="svgResource">The svg resource.</param>
    /// <param name="renderSurfaceHost">The render-surface host that owns the drawing.</param>
    /// <param name="view">The view used for presentation and coordinate conversion.</param>
    /// <param name="screenBounds">The bounds in screen pixels.</param>
    /// <param name="nickname">An optional name used to identify the object.</param>
    public DirectSvg(SvgResource svgResource,
                     RenderSurfaceHostBase renderSurfaceHost,
                     View view,
                     Rectangle screenBounds,
                     string? nickname = null)
        : this(svgResource, renderSurfaceHost, DirectDrawingMode.View, null, view, screenBounds, null, nickname)
    { }

    /// <summary>
    /// Sets the filter quality used when drawing the cached SVG bitmap.
    /// </summary>
    /// <param name="quality">The image encoding quality, from 0 through 100.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public DirectSvg SetFilterQuality(SKFilterQuality quality)
    {
        _paint.FilterQuality = quality;
        ForceRefresh();
        return this;
    }

    /// <inheritdoc />
    protected override void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        int width = Math.Max(1, (int)MathF.Round(destRectScreen.Width));
        int height = Math.Max(1, (int)MathF.Round(destRectScreen.Height));

        if (_cachedBitmap is null || _cachedWidth != width || _cachedHeight != height)
        {
            _cachedBitmap = _svgResource.Rasterize(width, height);
            _cachedWidth = width;
            _cachedHeight = height;
        }

        backbuffer.Canvas.DrawBitmap(_cachedBitmap, destRectScreen.ToPixelAlignedRect().ToSKRect(), _paint);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Bitmap lifetime is owned by SvgResource's per-size cache.
            _cachedBitmap = null;
            _paint.Dispose();
        }

        base.Dispose(disposing);
    }
}
