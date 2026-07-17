using Gondwana.Rendering;

namespace Gondwana.Widgets;

internal static class WidgetInputRouterRegistry
{
    private static readonly object _syncRoot = new();

    private static readonly Dictionary<RenderSurfaceHostBase, WidgetInputRouter> _routers =
            new(ReferenceEqualityComparer.Instance);

    internal static void Attach(RenderSurfaceHostBase renderSurfaceHost, WidgetInputRouter router)
    {
        ArgumentNullException.ThrowIfNull(renderSurfaceHost);
        ArgumentNullException.ThrowIfNull(router);

        lock (_syncRoot)
        {
            if (_routers.TryGetValue(renderSurfaceHost, out var existing) &&
                !ReferenceEquals(existing, router))
            {
                throw new InvalidOperationException(
                    "A widget input router is already attached to this render surface host.");
            }

            _routers[renderSurfaceHost] = router;
        }
    }

    internal static void Detach(RenderSurfaceHostBase renderSurfaceHost, WidgetInputRouter router)
    {
        lock (_syncRoot)
        {
            if (_routers.TryGetValue(renderSurfaceHost, out var existing) &&
                ReferenceEquals(existing, router))
            {
                _routers.Remove(renderSurfaceHost);
            }
        }
    }

    internal static bool TryRegister(WidgetBase widget)
    {
        ArgumentNullException.ThrowIfNull(widget);

        WidgetInputRouter? router;

        lock (_syncRoot)
        {
            _routers.TryGetValue(
                widget.RenderSurfaceHost,
                out router);
        }

        if (router is null)
            return false;

        router.Register(widget);
        return true;
    }

    internal static bool TryBringToFront(WidgetBase widget)
    {
        ArgumentNullException.ThrowIfNull(widget);

        WidgetInputRouter? router;

        lock (_syncRoot)
        {
            _routers.TryGetValue(
                widget.RenderSurfaceHost,
                out router);
        }

        if (router is null)
            return false;

        router.Register(widget);
        router.BringToFront(widget);

        return true;
    }
}