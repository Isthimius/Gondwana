using System.Drawing;
using SkiaSharp;

namespace Gondwana.Rendering;

/// <summary>
/// A rectangular window on the render target with its own zoom and placement.
/// Viewport never moves the world; it only scales and positions the drawing.
/// Camera "moves" the world (by pushing RenderSurfaceOriginPx to layers).
/// </summary>
public sealed class Viewport
{
    /// <summary>
    /// Screen-space rectangle (in RenderSurface pixels) where this view is drawn.
    /// This defines the on-screen position and size of the viewport for this view.
    /// </summary>
    public Rectangle TargetRectPx { get; set; } = new Rectangle(0, 0, 1280, 720);

    /// <summary>
    /// Zoom factor applied to the world when rendering this view. Values greater
    /// than 1 zoom in, values between 0 and 1 zoom out. Used when converting
    /// between screen-space and world-space pixels.
    /// </summary>
    public float Zoom { get; set; } = 1f;

    /// <summary>Optional per-view HUD/safe-area offset in screen pixels.</summary>
    public PointF ScreenOffsetPx { get; set; } = PointF.Empty;

    /// <summary>World size visible through this viewport (useful for Camera clamping).</summary>
    public SizeF VisibleWorldSizePx => new SizeF(TargetRectPx.Width * Zoom, TargetRectPx.Height * Zoom);

    /// <summary>
    /// Apply clip and transform for this viewport. Must be paired with End().
    /// NOTE: This intentionally does NOT translate by camera position; Camera already
    ///       pushed parallax origins into SceneLayers via RenderSurfaceOriginPx.
    /// </summary>
    internal void Begin(SKCanvas canvas)
    {
        canvas.Save();

        // Clip to viewport rectangle so views don't bleed into each other.
        canvas.ClipRect(new SKRect(TargetRectPx.Left, TargetRectPx.Top, TargetRectPx.Right, TargetRectPx.Bottom));

        // Move origin to the viewport rectangle’s top-left.
        canvas.Translate(TargetRectPx.Left + ScreenOffsetPx.X, TargetRectPx.Top + ScreenOffsetPx.Y);

        // Scale from world pixels -> screen pixels (1/Zoom).
        // Higher Zoom shows more of the world (zoom out).
        float s = 1f / Math.Max(Zoom, 1e-6f);
        canvas.Scale(s, s);
    }

    internal void End(SKCanvas canvas) => canvas.Restore();
}
