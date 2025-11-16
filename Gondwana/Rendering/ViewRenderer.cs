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

    internal void AddView(View v) => _views.Add(v);

    public void AddView(Rectangle targetRectPx, float zoom = 1f)
    {
        if (_renderSurfaceHost.Scene is not null)
        {
            var cam = new Camera(_renderSurfaceHost.Scene)
            {
                // TODO: this should be world pixel size, not adapter size
                // clamp camera to Scene pixel bounds
                //WorldBoundsPx = new RectangleF(0, 0, _renderSurfaceHost!.RenderSurfaceAdapter!.Width, _renderSurfaceHost.RenderSurfaceAdapter.Height),
                WorldBoundsPx = RectangleF.Empty,
                FollowLerpPerSecond = 0f // snap by default
            };

            cam.SnapTo(new PointF(0, 0));

            var vp = new Viewport
            {
                TargetRectPx = targetRectPx,
                Zoom = zoom
            };

            AddView(new View(cam, vp));
        }
    }

    public void ClearViews() => _views.Clear();

    internal void Render(SKCanvas canvas, float dtSeconds, System.Action<SKCanvas> drawScene)
    {
        // Update each camera, then draw each view with its own clip/scale.
        foreach (var v in _views)
        {
            v.Camera.Update(dtSeconds);
            v.Viewport.Begin(canvas);
            drawScene(canvas);   // Your existing Scene rendering (layers already offset by camera)
            v.Viewport.End(canvas);
        }
    }

    internal void BindToScene()
    {
        // new Scene, new Views
        ClearViews();

        if (_renderSurfaceHost.Scene is not null)
        {
            var cam = new Camera(_renderSurfaceHost.Scene)
            {
                // TODO: this should be world pixel size, not adapter size
                // clamp camera to Scene pixel bounds
                //WorldBoundsPx = new RectangleF(0, 0, _renderSurfaceHost!.RenderSurfaceAdapter!.Width, _renderSurfaceHost.RenderSurfaceAdapter.Height),
                WorldBoundsPx = RectangleF.Empty,
                FollowLerpPerSecond = 0f // snap by default
            };

            cam.SnapTo(new PointF(0, 0));

            var vp = new Viewport
            {
                TargetRectPx = new Rectangle(0, 0, _renderSurfaceHost.RenderSurfaceAdapter.Width, _renderSurfaceHost.RenderSurfaceAdapter.Height),
                Zoom = 1f
            };

            AddView(new View(cam, vp));
        }
    }
}
