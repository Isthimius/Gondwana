using System.Drawing;
using SkiaSharp;

namespace Gondwana.Rendering;

/// <summary>
/// A rectangular window on the render target with its own zoom and placement.
/// Viewport never moves the world; it only scales and positions the drawing.
/// </summary>
public sealed class Viewport
{
    private Rectangle _targetRectPx = new Rectangle(0, 0, 1280, 720);
    private float _zoom = 1f;

    /// <summary>
    /// Fired whenever <see cref="TargetRectPx"/> changes (viewport resized or moved).
    /// Provides the Viewport instance and the old and new rectangles.
    /// </summary>
    public event Action<ViewportResizedEventArgs>? TargetRectChanged;

    /// <summary>
    /// Occurs when the zoom level of the viewport changes.
    /// </summary>
    public event Action<ViewportZoomChangedEventArgs>? ZoomChanged;

    /// <summary>
    /// Screen-space rectangle (in RenderSurface pixels) where this view is drawn.
    /// This defines the on-screen position and size of the viewport for this view.
    /// </summary>
    public Rectangle TargetRectPx
    {
        get => _targetRectPx;
        set
        {
            if (value == _targetRectPx)
                return;

            var oldRect = _targetRectPx;
            _targetRectPx = value;
            TargetRectChanged?.Invoke(new ViewportResizedEventArgs(this, oldRect, value));
        }
    }

    /// <summary>
    /// Zoom factor applied to the world when rendering this view. Values greater
    /// than 1 zoom in, values between 0 and 1 zoom out. Used when converting
    /// between screen-space and world-space pixels.
    /// </summary>
    public float Zoom
    {
        get => _zoom;
        set
        {
            if (Math.Abs(value - _zoom) < 1e-6f)
                return;

            var oldZoom = _zoom;
            _zoom = value;
            ZoomChanged?.Invoke(new ViewportZoomChangedEventArgs(this, oldZoom, value));
        }
    }

    /// <summary>Optional per-view HUD/safe-area offset in screen pixels.</summary>
    public PointF ScreenOffsetPx { get; set; } = PointF.Empty;

    /// <summary>World size visible through this viewport (useful for Camera clamping).</summary>
    public SizeF VisibleWorldSizePx => new SizeF(TargetRectPx.Width / Zoom, TargetRectPx.Height / Zoom);

    /// <summary>
    /// Apply clip and transform for this viewport. Must be paired with End().
    /// Camera position is applied as a world-space offset.
    /// </summary>
    internal void Begin(SKCanvas canvas, PointF cameraPos)
    {
        canvas.Save();

        // Clip to viewport rect
        canvas.ClipRect(new SKRect(
            TargetRectPx.Left,
            TargetRectPx.Top,
            TargetRectPx.Right,
            TargetRectPx.Bottom));

        float zoom = Math.Max(Zoom, 1e-6f);
        float s = 1f / zoom;

        // Apply in this order (because Skia pre-concats):
        //   1) world -> world - camera
        //   2) scale by 1/zoom
        //   3) shift into viewport on the adapter
        canvas.Translate(-cameraPos.X, -cameraPos.Y);
        canvas.Scale(s, s);
        canvas.Translate(
            TargetRectPx.Left + ScreenOffsetPx.X,
            TargetRectPx.Top + ScreenOffsetPx.Y);
    }
    internal void End(SKCanvas canvas) => canvas.Restore();
}
