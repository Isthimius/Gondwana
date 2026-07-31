using System.Drawing;
using SkiaSharp;

namespace Gondwana.Rendering.Views;

/// <summary>
/// A rectangular window on the render target with its own zoom and placement.
/// Viewport never moves the world; it only scales and positions the drawing.
/// </summary>
public sealed class Viewport
{
    private Rectangle _targetRectPx = new Rectangle(0, 0, 1280, 720);
    private float _zoom = 1f;
    private float? _zoomTarget;
    private float _zoomLerpPerSecond;
    private float? _zoomDurationSeconds;
    private float _zoomElapsedSeconds;
    private float _zoomStart;

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
    /// Screen-space rectangle (in SCREEN / RenderSurface pixels) where this view is drawn.
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
    /// Updates the viewport size while preserving its current on-screen origin.
    /// This is a convenience around <see cref="TargetRectPx"/> that only changes
    /// the width and height.
    /// </summary>
    /// <param name="width">New viewport width in pixels.</param>
    /// <param name="height">New viewport height in pixels.</param>
    public void Resize(int width, int height)
    {
        var rect = TargetRectPx;
        TargetRectPx = new Rectangle(rect.Left, rect.Top, width, height);
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

    /// <summary>World size visible through this viewport (used for Camera clamping).</summary>
    public SizeF VisibleWorldSizePx => new SizeF(TargetRectPx.Width / Zoom, TargetRectPx.Height / Zoom);

    /// <summary>Gets whether a smooth zoom animation is currently active.</summary>
    internal bool IsZoomAnimating => _zoomTarget is not null;

    #region zoom zoom

    /// <summary>
    /// Instantly sets the zoom level, raising the ZoomChanged event.
    /// </summary>
    public void SnapZoom(float zoom)
    {
        _zoomTarget = null;
        _zoomLerpPerSecond = 0f;
        _zoomDurationSeconds = null;
        _zoomElapsedSeconds = 0f;
        _zoomStart = zoom;

        Zoom = zoom;
    }

    /// <summary>
    /// Smoothly animates the zoom toward a target level using an exponential
    /// lerp rate in "per second" units. Values &lt;= 0 snap immediately.
    /// </summary>
    public void ZoomTo(float targetZoom, float lerpPerSecond)
    {
        if (lerpPerSecond <= 0f)
        {
            SnapZoom(targetZoom);
            return;
        }

        _zoomStart = _zoom;
        _zoomTarget = targetZoom;
        _zoomLerpPerSecond = lerpPerSecond;
        _zoomDurationSeconds = null;
        _zoomElapsedSeconds = 0f;
    }

    /// <summary>
    /// Smoothly animates the zoom using exponential easing and snaps exactly to
    /// the target when the requested duration has elapsed. Values &lt;= 0 snap.
    /// </summary>
    public void ZoomToOverDuration(float targetZoom, float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            SnapZoom(targetZoom);
            return;
        }

        _zoomStart = _zoom;
        _zoomTarget = targetZoom;

        _zoomDurationSeconds = durationSeconds;
        _zoomElapsedSeconds = 0f;

        // The fixed-duration path calculates progress directly.
        _zoomLerpPerSecond = 0f;
    }

    /// <summary>
    /// Updates any in-progress zoom animation. Should be called once per frame
    /// with the elapsed time in seconds.
    /// </summary>
    internal void UpdateZoom(float dtSeconds)
    {
        if (_zoomTarget is null)
            return;

        float target = _zoomTarget.Value;
        float dt = Math.Max(0f, dtSeconds);

        if (_zoomDurationSeconds is { } durationSeconds)
        {
            _zoomElapsedSeconds = Math.Min(_zoomElapsedSeconds + dt, durationSeconds);

            float progress = durationSeconds > 0f ? _zoomElapsedSeconds / durationSeconds : 1f;

            // Preserve the existing exponential ease-out character, but normalize
            // it so progress reaches exactly 1 at the requested duration.
            const float decay = 4.605170186f; // -ln(0.01)

            float normalization = 1f - MathF.Exp(-decay);
            float easedProgress = (1f - MathF.Exp(-decay * progress)) / normalization;

            Zoom = _zoomStart + (target - _zoomStart) * easedProgress;

            if (_zoomElapsedSeconds >= durationSeconds)
            {
                // This no longer causes a visible jump: easedProgress is already 1.
                SnapZoom(target);
            }

            return;
        }

        if (_zoomLerpPerSecond <= 0f)
            return;

        float t = 1f - MathF.Exp(-_zoomLerpPerSecond * dt);

        float newZoom = _zoom + (target - _zoom) * t;

        Zoom = newZoom;

        if (Math.Abs(newZoom - target) < 1e-4f)
            SnapZoom(target);
    }
    #endregion zoom zoom
}