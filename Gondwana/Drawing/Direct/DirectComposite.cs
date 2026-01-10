using Gondwana.Movement;
using Gondwana.Rendering;
using Gondwana.Timers;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Numerics;

namespace Gondwana.Drawing.Direct;

public class DirectComposite : IDirectDrawable, IMovable
{
    private long _lastTick = HighResTimer.GetCurrentTick();

    private readonly List<DirectDrawingMovableBase> _children = new();
    private readonly Dictionary<DirectDrawingMovableBase, Vector2> _localOffsetPx = new();
    private PointF _anchor;

    public event EventHandler<IDirectDrawable>? Disposing;

    public DirectComposite(RenderSurfaceHostBase renderSurfaceHost, DirectDrawingMode mode, PointF anchor = default, string? nickname = null)
    {
        RenderSurfaceHost = renderSurfaceHost;
        Mode = mode;
        _anchor = anchor;
        Children = new ReadOnlyCollection<DirectDrawingMovableBase>(_children);
        Nickname = nickname;

        Movement = new MovementController(this, MovementState.ForPixel(new Vector2(_anchor.X, _anchor.Y)));
        DirectDrawingManager.Instance.AddOrReplace(this);
    }

    public RenderSurfaceHostBase RenderSurfaceHost { get; }
    public ReadOnlyCollection<DirectDrawingMovableBase> Children { get; }
    public MovementController Movement { get; private set; }

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
            ? new Vector2(child.ScreenBounds.X - _anchor.X, child.ScreenBounds.Y - _anchor.Y)
            : (explicitLocalOffsetPx ?? Vector2.Zero);

        _localOffsetPx[child] = offset;

        // Normalize immediately
        child.SetPosition(new Vector2(_anchor.X, _anchor.Y) + offset);
        return this;
    }

    public DirectComposite Remove(DirectDrawingMovableBase child)
    {
        if (child is null) throw new ArgumentNullException(nameof(child));
        _children.Remove(child);
        _localOffsetPx.Remove(child);
        return this;
    }

    private void OnChildDisposing(object? sender, IDirectDrawable drawing)
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
        child.SetPosition(new Vector2(_anchor.X, _anchor.Y) + newLocalOffsetPx);
        return this;
    }

    /// <summary>Set the composite anchor in pixels and reposition all children from their stored local offsets.</summary>
    public DirectComposite SetPosition(float x, float y)
    {
        _anchor = new PointF(x, y);
        var anchorV = new Vector2(x, y);

        foreach (var c in _children)
        {
            var off = _localOffsetPx.TryGetValue(c, out var v) ? v : Vector2.Zero;
            c.SetPosition(anchorV + off);
        }

        return this;
    }

    #region IMovable members

    // IMovable (pixel space only)
    public MovementSpace PositionSpace => MovementSpace.Pixel;
    
    public Vector2 GetPosition() => new(_anchor.X, _anchor.Y);

    public void SetPosition(Vector2 pos) => SetPosition(pos.X, pos.Y);

    #endregion IMovable members

    // Bounding box = union of visible children (unchanged)
    public Rectangle ScreenBounds
    {
        get
        {
            if (_children.Count == 0)
                return Rectangle.Empty;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var child in _children)
            {
                if (!child.Visible)
                    continue;

                var b = child.ScreenBounds;

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

            if (minX == float.MaxValue) return Rectangle.Empty;
            return Rectangle.FromLTRB(
                (int)Math.Floor(minX),
                (int)Math.Floor(minY),
                (int)Math.Ceiling(maxX),
                (int)Math.Ceiling(maxY));
        }
    }

    public string? Nickname { get; private set; }

    public int ZOrder => 0;

    public DirectDrawingMode Mode { get; }

    /// <summary>
    /// Returns true if any child is visible; setting this sets all children's Visible to the same value.
    /// </summary>
    public bool Visible
    {
        get
        {
            foreach (var c in _children)
            {
                if (c.Visible)
                    return true;
            }

            return false;
        }
        set
        {
            foreach (var c in _children)
            {
                c.Visible = value;
            }
        }
    }

    public Guid Id { get; } = Guid.NewGuid();

    public void Draw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        // Intentionally no-op.
        // Composite is a grouping/controller object; children are responsible for rendering.
    }

    public void Update(long tick)
    {
        Movement.AdvanceMovement(HighResTimer.GetDuration(_lastTick, tick));
        _lastTick = tick;
    }

    // Group ops passthroughs
    public DirectComposite SetGroupZOrder(int z)
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
            c.Visible = visible;

        return this;
    }

    public void Dispose()
    {
        Disposing?.Invoke(this, this);

        foreach (var c in _children)
            c.Dispose();

        _children.Clear();
        _localOffsetPx.Clear();
    }

    public RectangleF GetDrawLocationScreen(View view)
    {
        throw new NotImplementedException();
    }
}
