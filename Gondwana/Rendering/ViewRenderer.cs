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
                _renderSurfaceHost.Scene.RefreshNeeded = SceneRefreshType.All;
        }
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
            _renderSurfaceHost.Scene.RefreshNeeded = SceneRefreshType.All;
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
            _renderSurfaceHost.Scene.RefreshNeeded = SceneRefreshType.All;
    }

    private void OnViewportTargetRectChanged(ViewportResizedEventArgs args)
    {
        if (_renderSurfaceHost.Scene is not null)
            _renderSurfaceHost.Scene.RefreshNeeded = SceneRefreshType.All;
    }

    #endregion private methods
}
