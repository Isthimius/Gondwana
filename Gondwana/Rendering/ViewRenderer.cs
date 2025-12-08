using Gondwana.Scenes;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Rendering;

public sealed class ViewRenderer
{
    private readonly RenderSurfaceHostBase _renderSurfaceHost;
    private readonly List<View> _views = new();

    private ViewRenderer() { }

    internal ViewRenderer(RenderSurfaceHostBase renderSurfaceHost)
    {
        _renderSurfaceHost = renderSurfaceHost;
    }

    public IReadOnlyList<View> Views => _views;

    public void AddView(Rectangle targetRectPx, float zoom = 1f, int zOrder = 0, RectangleF? worldBoundsPx = null)
    {
        if (_renderSurfaceHost.Scene is not null)
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

            if (_renderSurfaceHost.Scene is not null)
                _renderSurfaceHost.Scene.FullRefreshNeeded = true;
        }
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

    public void ClearViews()
    {
        foreach (var view in _views)
        {
            view.Viewport.TargetRectChanged -= OnViewportTargetRectChanged;
            view.Viewport.ZoomChanged -= OnViewportZoomChanged;
        }

        _views.Clear();

        if (_renderSurfaceHost.Scene is not null)
            _renderSurfaceHost.Scene.FullRefreshNeeded = true;
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

    internal void Render(
        SKCanvas canvas,
        float dtSeconds,
        Scene scene,
        Action<View, SceneLayer> drawLayer)
    {
        foreach (var view in _views.OrderBy(v => v.ZOrder))
        {
            // Camera already updated earlier this frame.
            view.Viewport.Begin(canvas);

            var cam = view.Camera;

            int countOfVisibleLayers = scene?.CountOfVisibleLayers ?? 0;
            for (int i = 0; i < countOfVisibleLayers; i++)
            {
                var layer = scene.VisibleSceneLayers[i];

                canvas.Save();

                float p = layer.Parallax;

                canvas.Translate(
                    -cam.PositionPx.X * p,
                    -cam.PositionPx.Y * p);

                drawLayer(view, layer);

                canvas.Restore();
            }

            view.Viewport.End(canvas);
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

    #endregion private methods
}
