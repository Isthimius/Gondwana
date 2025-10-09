using Gondwana.Rendering;
using System.Drawing;

namespace Gondwana.Drawing.Direct;

public class DirectComposite
{
    private readonly List<DirectDrawingBase> _children = new();
    private PointF _anchor;

    public DirectComposite(RenderSurfaceHostBase renderSurfaceHost, PointF anchor = new PointF())
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

    public Rectangle Bounds
    {
        get
        {
            if (_children.Count == 0)
                return Rectangle.Empty;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var child in _children)
            {
                if (!child.IsVisible)
                    continue;       // skip invisible children

                var b = child.Bounds;
                if (b == Rectangle.Empty)
                    continue;

                if (b.Left < minX)
                    minX = b.Left;

                if (b.Top < minY)
                    minY = b.Top;

                if (b.Right > maxX)
                    maxX = b.Right;

                if (b.Bottom > maxY)
                    maxY = b.Bottom;
            }

            if (minX == float.MaxValue)
                return Rectangle.Empty; // all children were empty/invisible

            return Rectangle.FromLTRB(
                (int)Math.Floor(minX),
                (int)Math.Floor(minY),
                (int)Math.Ceiling(maxX),
                (int)Math.Ceiling(maxY)
            );
        }
    }

    public DirectComposite SetZOrder(int z)
    {
        foreach (var c in _children)
            c.ZOrder = z;

        return this;
    }

    public DirectComposite SetOpacity(float opacity)
    {
        foreach (var c in _children)
            c.Opacity = opacity;

        return this;
    }

    public DirectComposite FadeTo(float targetOpacity, float durationSec)
    {
        foreach (var c in _children)
            c.FadeTo(targetOpacity, durationSec);

        return this;
    }

    public DirectComposite FadeIn(float durationSec)
    {
        foreach (var c in _children)
            c.FadeIn(durationSec);

        return this;
    }

    public DirectComposite FadeOut(float durationSec)
    {
        foreach (var c in _children)
            c.FadeOut(durationSec);

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
