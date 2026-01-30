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
    internal static RenderContext? Current => _current;

    private readonly RenderContext? _prior;

    private RenderContext(View view, long tick, RenderContext? prior)
    {
        View = view;
        Tick = tick;

        var z = view?.Viewport?.Zoom ?? 1f;
        ViewportZoom = (z > 0f) ? z : 1f;

        _prior = prior;
    }

    internal View View { get; }
    internal long Tick { get; }
    internal float ViewportZoom { get; }

    internal static void Push(View view, long tick)
    {
        _current = new RenderContext(view, tick, _current);
    }

    internal static void Pop()
    {
        _current = _current?._prior;
    }
}
