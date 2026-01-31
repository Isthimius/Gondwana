using Gondwana.Scenes;
using System.Collections.ObjectModel;
using System.Drawing;

namespace Gondwana.Rendering.Views;

/// <summary>
/// Manages a collection of views for a render surface, handling view creation,
/// removal, ordering, and multi-viewport layouts. Each view combines a camera
/// and viewport to control what portion of the scene is rendered and where it
/// appears on screen.
/// </summary>
public sealed class ViewManager
{
    private readonly RenderSurfaceHostBase _renderSurfaceHost;
    private readonly List<View> _views = new();

    // TODO: add events for view added/removed/changed

    private ViewManager() { }

    internal ViewManager(RenderSurfaceHostBase renderSurfaceHost)
    {
        _renderSurfaceHost = renderSurfaceHost;
    }

    /// <summary>
    /// Gets a read-only collection of all views currently managed by this view manager.
    /// Views are sorted by Z-order, with lower values appearing first (drawn behind).
    /// </summary>
    /// <value>
    /// A <see cref="ReadOnlyCollection{T}"/> of <see cref="View"/> instances.
    /// </value>
    public ReadOnlyCollection<View> Views => _views.AsReadOnly();

    /// <summary>
    /// Creates and adds a new view with the specified screen rectangle, zoom level,
    /// and Z-order. The view is automatically configured with a camera that can be
    /// optionally clamped to world bounds.
    /// </summary>
    /// <param name="targetRectPx">
    /// The screen-space rectangle (in pixels) where this view will be rendered
    /// on the render surface.
    /// </param>
    /// <param name="zoom">
    /// Initial zoom factor for the view. Values greater than 1 zoom in;
    /// values less than 1 zoom out. Default is 1 (no zoom).
    /// </param>
    /// <param name="zOrder">
    /// Draw order relative to other views. Lower values are drawn first (behind);
    /// higher values are drawn later (in front). Default is 0.
    /// </param>
    /// <param name="worldBoundsPx">
    /// Optional world-space bounds (in pixels) that limit where the camera can move.
    /// If null or <see cref="RectangleF.Empty"/>, the camera has no movement constraints.
    /// </param>
    /// <remarks>
    /// The newly created view's camera is initially positioned at world origin (0,0)
    /// and configured to snap to targets without smoothing. The view is automatically
    /// sorted by Z-order after being added.
    /// </remarks>
    public void AddView(Rectangle targetRectPx, float zoom = 1f, int zOrder = 0, RectangleF? worldBoundsPx = null)
    {
        if (worldBoundsPx is null)
            worldBoundsPx = RectangleF.Empty;

        var cam = new Camera(_renderSurfaceHost.Scene)
        {
            // clamp camera to Scene pixel bounds
            WorldBoundsPx = worldBoundsPx.Value,
            FollowLerpPerSecond = 0f // snap by default
        };

        cam.SnapTo(new PointF(0, 0));

        var vp = new Viewport
        {
            TargetRectPx = targetRectPx,
            Zoom = zoom
        };

        var view = new View(cam, vp) { ZOrder = zOrder };
        view.Viewport.TargetRectChanged += OnViewportTargetRectChanged;
        view.Viewport.ZoomChanged += OnViewportZoomChanged;

        _views.Add(view);
        _renderSurfaceHost.Scene.FullRefreshNeeded = true;

        SortViews();
    }

    /// <summary>
    /// Removes all existing views and creates a single full-screen view that
    /// occupies the entire render surface adapter.
    /// </summary>
    /// <param name="zoom">Initial zoom level for the view.</param>
    /// <param name="zOrder">Z-order for the view (default 0).</param>
    public void ConfigureSingleFullView(float zoom = 1f, int zOrder = 0)
    {
        if (_renderSurfaceHost.RenderSurfaceAdapter is null)
            throw new InvalidOperationException("RenderSurfaceAdapter is not available.");

        ClearViews();

        var adapter = _renderSurfaceHost.RenderSurfaceAdapter;
        var bounds = new Rectangle(0, 0, adapter.Width, adapter.Height);

        RectangleF worldBoundsPx = RectangleF.Empty;
        if (_renderSurfaceHost.Scene is not null)
            worldBoundsPx = _renderSurfaceHost.Scene.GetWorldBoundsPx();

        AddView(bounds, zoom, zOrder, worldBoundsPx);
    }

    /// <summary>
    /// Removes all existing views and creates a vertical split-screen layout:
    /// left and right views sharing the full height of the render surface.
    /// </summary>
    /// <param name="leftZoom">Initial zoom for the left view.</param>
    /// <param name="rightZoom">Initial zoom for the right view.</param>
    public void ConfigureVerticalSplit(float leftZoom = 1f, float rightZoom = 1f)
    {
        if (_renderSurfaceHost.RenderSurfaceAdapter is null)
            throw new InvalidOperationException("RenderSurfaceAdapter is not available.");

        ClearViews();

        var adapter = _renderSurfaceHost.RenderSurfaceAdapter;
        int width = adapter.Width;
        int height = adapter.Height;

        int halfWidth = width / 2;

        RectangleF worldBoundsPx = RectangleF.Empty;
        if (_renderSurfaceHost.Scene is not null)
            worldBoundsPx = _renderSurfaceHost.Scene.GetWorldBoundsPx();

        // Left view
        AddView(
            new Rectangle(0, 0, halfWidth, height),
            leftZoom,
            zOrder: 0,
            worldBoundsPx: worldBoundsPx);

        // Right view
        AddView(
            new Rectangle(halfWidth, 0, width - halfWidth, height),
            rightZoom,
            zOrder: 1,
            worldBoundsPx: worldBoundsPx);
    }

    /// <summary>
    /// Removes all existing views and creates a horizontal split-screen layout:
    /// top and bottom views sharing the full width of the render surface.
    /// </summary>
    /// <param name="topZoom">Initial zoom for the top view.</param>
    /// <param name="bottomZoom">Initial zoom for the bottom view.</param>
    public void ConfigureHorizontalSplit(float topZoom = 1f, float bottomZoom = 1f)
    {
        if (_renderSurfaceHost.RenderSurfaceAdapter is null)
            throw new InvalidOperationException("RenderSurfaceAdapter is not available.");

        ClearViews();

        var adapter = _renderSurfaceHost.RenderSurfaceAdapter;
        int width = adapter.Width;
        int height = adapter.Height;

        int halfHeight = height / 2;

        RectangleF worldBoundsPx = RectangleF.Empty;
        if (_renderSurfaceHost.Scene is not null)
            worldBoundsPx = _renderSurfaceHost.Scene.GetWorldBoundsPx();

        // Top view
        AddView(
            new Rectangle(0, 0, width, halfHeight),
            topZoom,
            zOrder: 0,
            worldBoundsPx: worldBoundsPx);

        // Bottom view
        AddView(
            new Rectangle(0, halfHeight, width, height - halfHeight),
            bottomZoom,
            zOrder: 1,
            worldBoundsPx: worldBoundsPx);
    }

    /// <summary>
    /// Removes all views from the manager, unsubscribing from their events and
    /// marking the scene as needing a full refresh.
    /// </summary>
    /// <remarks>
    /// This method cleanly detaches event handlers from each view's viewport
    /// before clearing the collection. After clearing, the scene is flagged
    /// for a full refresh to update the rendering state.
    /// </remarks>
    public void ClearViews()
    {
        foreach (var view in _views!)
        {
            view.Viewport.TargetRectChanged -= OnViewportTargetRectChanged;
            view.Viewport.ZoomChanged -= OnViewportZoomChanged;
        }

        _views.Clear();

        if (_renderSurfaceHost.Scene is not null)
            _renderSurfaceHost.Scene.FullRefreshNeeded = true;
    }

    /// <summary>
    /// Returns all views that are rendered behind (below) the specified view,
    /// based on Z-order. Views with lower Z-order values appear behind.
    /// </summary>
    /// <param name="view">
    /// The reference view to find views below.
    /// </param>
    /// <returns>
    /// A read-only list of views with lower Z-order than the specified view.
    /// Returns an empty collection if the view is not found or is already
    /// the bottom-most view.
    /// </returns>
    public IReadOnlyList<View> GetViewsBelow(View view)
    {
        int idx = _views.IndexOf(view);
        if (idx <= 0)
            return Array.Empty<View>();

        // views with lower Z (earlier in sorted list)
        return _views.GetRange(0, idx);
    }

    /// <summary>
    /// Returns all views that are rendered in front of (above) the specified view,
    /// based on Z-order. Views with higher Z-order values appear in front.
    /// </summary>
    /// <param name="view">
    /// The reference view to find views above.
    /// </param>
    /// <returns>
    /// A read-only list of views with higher Z-order than the specified view.
    /// Returns an empty collection if the view is not found or is already
    /// the top-most view.
    /// </returns>
    public IReadOnlyList<View> GetViewsAbove(View view)
    {
        int idx = _views.IndexOf(view);
        if (idx < 0 || idx >= _views.Count - 1)
            return Array.Empty<View>();

        // views with higher Z (later in sorted list)
        return _views.GetRange(idx + 1, _views.Count - (idx + 1));
    }

    #region internal methods

    internal void UpdateCameras(float dtSeconds)
    {
        foreach (var view in _views)
        {
            view.Camera.Update(dtSeconds);
            view.Viewport.UpdateZoom(dtSeconds);
        }
    }

    internal void BindToScene(Scene scene, bool limitCameraToWorldBoundPx)
    {
        RectangleF worldBoundsPx = limitCameraToWorldBoundPx ? scene.GetWorldBoundsPx() : RectangleF.Empty;

        // if no views exist, create a default one
        if (!_views.Any())
        {
            AddView(new Rectangle(0, 0, _renderSurfaceHost.RenderSurfaceAdapter!.Width, _renderSurfaceHost.RenderSurfaceAdapter.Height), 1, 0, worldBoundsPx);
        }
        else
        {
            foreach (var view in _views)
            {
                view.Camera.WorldBoundsPx = worldBoundsPx;
            }
        }
    }

    #endregion internal methods

    #region private methods

    private void OnViewportZoomChanged(ViewportZoomChangedEventArgs args)
    {
        if (_renderSurfaceHost.Scene is not null)
            _renderSurfaceHost.Scene.FullRefreshNeeded = true;
    }

    private void OnViewportTargetRectChanged(ViewportResizedEventArgs args)
    {
        if (_renderSurfaceHost.Scene is not null)
            _renderSurfaceHost.Scene.FullRefreshNeeded = true;
    }

    private void SortViews()
    {
        _views.Sort(static (a, b) =>
        {
            int cmp = a.ZOrder.CompareTo(b.ZOrder);

            if (cmp != 0)
                return cmp;

            // deterministic tie-breaker
            return a.Id.CompareTo(b.Id);
        });
    }

    #endregion private methods
}
