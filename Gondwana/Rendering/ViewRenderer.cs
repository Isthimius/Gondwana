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
                // TODO: this should be world pixel size, not adapter size
                // clamp camera to Scene pixel bounds
                //WorldBoundsPx = new RectangleF(0, 0, _renderSurfaceHost!.RenderSurfaceAdapter!.Width, _renderSurfaceHost.RenderSurfaceAdapter.Height),
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
                _renderSurfaceHost.Scene.RefreshNeeded = SceneRefreshType.All;
        }
    }

    private void OnViewportZoomChanged(ViewportZoomChangedEventArgs args)
    {
        if (_renderSurfaceHost.Scene is not null)
            _renderSurfaceHost.Scene.RefreshNeeded = SceneRefreshType.All;
    }

    private void OnViewportTargetRectChanged(ViewportResizedEventArgs args)
    {
        if (_renderSurfaceHost.Scene is not null)
            _renderSurfaceHost.Scene.RefreshNeeded = SceneRefreshType.All;
    }

    public void ClearViews()
    {
        foreach (var v in _views)
        {
            v.Viewport.TargetRectChanged -= OnViewportTargetRectChanged;
            v.Viewport.ZoomChanged -= OnViewportZoomChanged;
        }

        _views.Clear();

        if (_renderSurfaceHost.Scene is not null)
            _renderSurfaceHost.Scene.RefreshNeeded = SceneRefreshType.All;
    }

    internal void Render(SKCanvas canvas, float dtSeconds, Action<SKCanvas> drawScene)
    {
        // Update each camera, then draw each view with its own clip/scale,
        // in ascending ZOrder (back -> front).
        foreach (var v in _views.OrderBy(v => v.ZOrder))
        {
            v.Camera.Update(dtSeconds);
            v.Viewport.Begin(canvas, v.Camera.PositionPx);
            drawScene(canvas);
            v.Viewport.End(canvas);
        }
    }

    internal void BindToScene(Scene scene, bool limitCameraToWorldBoundPx)
    {
        RectangleF worldBoundsPx = limitCameraToWorldBoundPx ? scene.GetWorldBoundsPx() : new RectangleF(0, 0, float.MaxValue, float.MaxValue);

        // if no views exist, create a default one
        if (!_views.Any())
        {
            AddView(new Rectangle(0, 0, _renderSurfaceHost.RenderSurfaceAdapter!.Width, _renderSurfaceHost.RenderSurfaceAdapter.Height), 1, 0, worldBoundsPx);
        }
        else
        {
            foreach (var v in _views)
            {
                v.Camera.WorldBoundsPx = worldBoundsPx;
            }
        }
    }
}
