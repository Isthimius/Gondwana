using Gondwana.Scenes;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Rendering;

public sealed class ViewRenderer
{
    private readonly List<View> _views = new();

    private ViewRenderer() { }

    public ViewRenderer(Scene scene)
    {
    }

    public IReadOnlyList<View> Views => _views;

    public void AddView(View v) => _views.Add(v);

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

    /// <summary>
    /// Ensure at least one view exists. If none are configured, create a full-screen
    /// default View (Camera + Viewport) bound to the current Scene and adapter size.
    /// </summary>
    private void EnsureDefaultView(Scene scene)
    {
        if (Views.Count > 0)
            return;

        var cam = new Camera(scene)
        {
            // clamp camera to Scene pixel bounds
            WorldBoundsPx = new RectangleF(0, 0, RenderSurfaceAdapter.Width, RenderSurfaceAdapter.Height),
            FollowLerpPerSecond = 0f // snap by default
        };

        cam.SnapTo(new PointF(0, 0));

        var vp = new Viewport
        {
            TargetRectPx = new Rectangle(0, 0, RenderSurfaceAdapter.Width, RenderSurfaceAdapter.Height),
            Zoom = 1f
        };

        ViewRenderer.AddView(new View(cam, vp));
    }
}
