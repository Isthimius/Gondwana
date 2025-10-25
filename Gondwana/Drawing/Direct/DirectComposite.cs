using Gondwana.Movement;
using Gondwana.Rendering;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Numerics;

namespace Gondwana.Drawing.Direct;

public class DirectComposite : IMovable
{
    private readonly List<DirectDrawingMovableBase> _children = new();
    private readonly Dictionary<DirectDrawingMovableBase, Vector2> _localOffsetPx = new();
    private PointF _anchor;

    public DirectComposite(RenderSurfaceHostBase renderSurfaceHost, PointF anchor = default)
    {
        RenderSurfaceHost = renderSurfaceHost;
        _anchor = anchor;
        Children = new ReadOnlyCollection<DirectDrawingMovableBase>(_children);
    }

    public RenderSurfaceHostBase RenderSurfaceHost { get; }
    public ReadOnlyCollection<DirectDrawingMovableBase> Children { get; }

    /// <summary>
    /// Adds a child and stores its local pixel offset from the composite anchor.
    /// If <paramref name="keepCurrentOffset"/> is true (default), the offset is computed from the child's current Bounds.
    /// Otherwise, pass an explicit local pixel offset.
    /// </summary>
    public DirectComposite Add(DirectDrawingMovableBase child, bool keepCurrentOffset = true, Vector2? explicitLocalOffsetPx = null)
    {
        if (child is null)
            throw new ArgumentNullException(nameof(child));

        if (child.RenderSurfaceHost != RenderSurfaceHost)
            throw new ArgumentException("Child's RenderSurfaceHost must match the Composite's RenderSurfaceHost.", nameof(child));

        if (_children.Contains(child))
            return this;

        _children.Add(child);
        child.Disposing += OnChildDisposing;

        Vector2 offset = keepCurrentOffset
            ? new Vector2(child.Bounds.X - _anchor.X, child.Bounds.Y - _anchor.Y)
            : (explicitLocalOffsetPx ?? Vector2.Zero);

        _localOffsetPx[child] = offset;

        // Normalize immediately
        child.Movement.SetPosition(new Vector2(_anchor.X, _anchor.Y) + offset);
        return this;
    }

    public DirectComposite Remove(DirectDrawingMovableBase child)
    {
        if (child is null) throw new ArgumentNullException(nameof(child));
        _children.Remove(child);
        _localOffsetPx.Remove(child);
        return this;
    }

    private void OnChildDisposing(object? sender, DirectDrawingBase drawing)
    {
        if (drawing is DirectDrawingMovableBase m)
        {
            _children.Remove(m);
            _localOffsetPx.Remove(m);
        }
    }

    // In Clear(), unsubscribe correctly:
    public DirectComposite Clear()
    {
        foreach (var c in _children)
            c.Disposing -= OnChildDisposing;

        _children.Clear();
        _localOffsetPx.Clear();
        return this;
    }

    /// <summary>Change a specific child's local pixel offset and re-apply its absolute position.</summary>
    public DirectComposite SetLocalOffset(DirectDrawingMovableBase child, Vector2 newLocalOffsetPx)
    {
        if (!_children.Contains(child)) return this;
        _localOffsetPx[child] = newLocalOffsetPx;
        child.Movement.SetPosition(new Vector2(_anchor.X, _anchor.Y) + newLocalOffsetPx);
        return this;
    }

    /// <summary>Move the composite anchor by a pixel delta and reposition all children.</summary>
    public DirectComposite MoveBy(float dx, float dy) => SetPosition(_anchor.X + dx, _anchor.Y + dy);

    /// <summary>Set the composite anchor in pixels and reposition all children from their stored local offsets.</summary>
    public DirectComposite SetPosition(float x, float y)
    {
        _anchor = new PointF(x, y);
        var anchorV = new Vector2(x, y);

        foreach (var c in _children)
        {
            var off = _localOffsetPx.TryGetValue(c, out var v) ? v : Vector2.Zero;
            c.Movement.SetPosition(anchorV + off); // child keeps MovementState↔Bounds in sync
        }
        return this;
    }

    // IMovable (pixel space only)
    public CoordinateSpace PositionSpace => CoordinateSpace.Pixel;
    public Vector2 GetPosition() => new(_anchor.X, _anchor.Y);
    public void SetPosition(Vector2 pos) => SetPosition(pos.X, pos.Y);

    // Group follow fan-outs: children do all grid↔pixel work; composite stays pixel-only.
    public DirectComposite FollowHard(IMovableOnSceneLayer gridTarget, Vector2 sharedGridOffset = default)
    {
        foreach (var c in _children) c.Movement.FollowHard(gridTarget, sharedGridOffset);
        return this;
    }

    public DirectComposite FollowSoft(IMovableOnSceneLayer gridTarget, float speedTilesPerSec,
                                      float snapEpsilon = 0.25f, Vector2 sharedGridOffset = default)
    {
        foreach (var c in _children) c.Movement.FollowSoft(gridTarget, speedTilesPerSec, snapEpsilon, sharedGridOffset);
        return this;
    }

    public DirectComposite UnfollowAll()
    {
        foreach (var c in _children) c.Movement.Unfollow();
        return this;
    }

    // Bounding box = union of visible children (unchanged)
    public Rectangle Bounds
    {
        get
        {
            if (_children.Count == 0) return Rectangle.Empty;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var child in _children)
            {
                if (!child.IsVisible) continue;
                var b = child.Bounds;
                if (b == Rectangle.Empty) continue;

                if (b.Left < minX) minX = b.Left;
                if (b.Top < minY) minY = b.Top;
                if (b.Right > maxX) maxX = b.Right;
                if (b.Bottom > maxY) maxY = b.Bottom;
            }

            if (minX == float.MaxValue) return Rectangle.Empty;
            return Rectangle.FromLTRB(
                (int)Math.Floor(minX),
                (int)Math.Floor(minY),
                (int)Math.Ceiling(maxX),
                (int)Math.Ceiling(maxY));
        }
    }

    // Group ops passthroughs
    public DirectComposite SetZOrder(int z) { foreach (var c in _children) c.ZOrder = z; return this; }
    public DirectComposite SetOpacity(float opacity) { foreach (var c in _children) c.Opacity = opacity; return this; }
    public DirectComposite FadeTo(float targetOpacity, float durationSec) { foreach (var c in _children) c.FadeTo(targetOpacity, durationSec); return this; }
    public DirectComposite FadeIn(float durationSec) { foreach (var c in _children) c.FadeIn(durationSec); return this; }
    public DirectComposite FadeOut(float durationSec) { foreach (var c in _children) c.FadeOut(durationSec); return this; }
    public DirectComposite SetIsVisible(bool visible) { foreach (var c in _children) c.IsVisible = visible; return this; }

    /// <summary>Dispose all children and clear the composite.</summary>
    public void DisposeAll()
    {
        foreach (var c in _children) c.Dispose();
        _children.Clear();
        _localOffsetPx.Clear();
    }
}
