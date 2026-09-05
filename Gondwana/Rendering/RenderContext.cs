using Gondwana.Rendering.Views;

namespace Gondwana.Rendering;

/// <summary>
/// Ambient per-render-pass context (per thread).
/// Set once per View render pass in RenderSurfaceHost.
/// SceneLayer-mode drawables can query this when they don't hold a View reference.
/// </summary>
internal sealed class RenderContext
{
    [ThreadStatic]
    private static RenderContext? _current;
    private static long _nextPassId;

    internal static RenderContext? Current => _current;

    private readonly RenderContext? _prior;

    private RenderContext(View view, long tick, RenderContext? prior)
    {
        PassId = System.Threading.Interlocked.Increment(ref _nextPassId);
        View = view;
        Tick = tick;

        // Capture every value used by the view's world/screen transform. GPU
        // rendering occurs on the UI/GL thread while the engine thread continues
        // updating cameras and animated viewports, so reading these values live
        // while drawing can produce multiple transforms within one frame.
        CameraPositionPx = view.Camera.PositionPx;
        ViewportTargetRectPx = view.Viewport.TargetRectPx;
        ViewportScreenOffsetPx = view.Viewport.ScreenOffsetPx;
        ViewEffectOffsetFactor = view.EffectOffsetFactor;
        ViewEffectOffsetPx = view.EffectOffsetPx;

        var z = view.Viewport.Zoom;
        ViewportZoom = (z > 0f) ? z : 1f;

        _prior = prior;
    }

    internal long PassId { get; }
    internal View View { get; }
    internal long Tick { get; }
    internal System.Drawing.PointF CameraPositionPx { get; }
    internal System.Drawing.Rectangle ViewportTargetRectPx { get; }
    internal System.Drawing.PointF ViewportScreenOffsetPx { get; }
    internal float ViewportZoom { get; }
    internal System.Drawing.PointF ViewEffectOffsetFactor { get; }
    internal System.Drawing.PointF ViewEffectOffsetPx { get; }

    internal static void Push(View view, long tick)
    {
        _current = new RenderContext(view, tick, _current);
    }

    internal static void Pop()
    {
        _current = _current?._prior;
    }
}
