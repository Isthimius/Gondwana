using Gondwana.Rendering;
using System.Drawing;

namespace Gondwana.Drawing.Direct;

public class DirectComposite
{
    private readonly List<DirectDrawingBase> _children = new();
    private PointF _anchor;

    public DirectComposite(RenderSurfaceHostBase renderSurfaceHost, PointF anchor)
    {
        RenderSurfaceHost = renderSurfaceHost;
        _anchor = anchor;
    }

    public RenderSurfaceHostBase RenderSurfaceHost { get; private set; }

    public PointF Anchor
    {
        get => _anchor;
        set => SetAnchor(value.X, value.Y);
    }

    public DirectComposite SetAnchor(float x, float y)
    {
        var dx = x - _anchor.X;
        var dy = y - _anchor.Y;
        _anchor = new PointF(x, y);

        // shift all children’s Bounds
        foreach (var c in _children)
        {
            var b = c.Bounds;
            c.Bounds = new Rectangle((int)(b.X + dx), (int)(b.Y + dy), b.Width, b.Height);
        }

        return this;
    }

    public DirectComposite Add(DirectDrawingBase child)
    {
        if (child?.RenderSurfaceHost != RenderSurfaceHost)
            throw new ArgumentException("Child's RenderSurfaceHost must match the Composite's RenderSurfaceHost.", nameof(child));

        _children.Add(child);
        return this;
    }

    public DirectComposite SetZOrder(int z)
    {
        foreach (var c in _children)
            c.ZOrder = z;

        return this;
    }

    public DirectComposite SetIsVisible(bool visible)
    {
        foreach (var c in _children)
            c.IsVisible = visible;

        return this;
    }

    public void DisposeAll()
    {
        foreach (var c in _children)
            c.Dispose();

        _children.Clear();
    }
}
