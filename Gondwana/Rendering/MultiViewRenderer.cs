using SkiaSharp;

namespace Gondwana.Rendering;

public sealed class MultiViewRenderer
{
    private readonly List<View> _views = new();

    public IReadOnlyList<View> Views => _views;

    public void AddView(View v) => _views.Add(v);

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
}
