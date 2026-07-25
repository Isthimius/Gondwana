using Gondwana.Physics.Movement;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Timers;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Numerics;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Represents a movable composite that owns other movable direct drawables and
/// keeps their positions relative to a shared anchor.
/// </summary>
/// <remarks>
/// <para>
/// A composite may recursively contain other composites. Every child must use
/// the same render surface, drawing mode, and target <see cref="View"/> or
/// <see cref="SceneLayer"/>.
/// </para>
/// <para>
/// A child may belong to only one composite at a time. Cyclic parent/child
/// relationships are rejected.
/// </para>
/// </remarks>
public class DirectComposite : IDirectDrawable, IMovable
{
    private static readonly object _parentSyncRoot = new();

    private static readonly Dictionary<IDirectDrawable, DirectComposite> _parents =
        new(ReferenceEqualityComparer.Instance);

    private readonly List<IDirectDrawable> _children = [];
    private readonly Dictionary<IDirectDrawable, Vector2> _localOffsetPx =
        new(ReferenceEqualityComparer.Instance);

    private readonly ReadOnlyCollection<IDirectDrawable> _readOnlyChildren;

    private PointF _anchor;
    private long _lastTick = HighResTimer.GetCurrentTick();
    private bool _disposed;

    /// <summary>
    /// Occurs when the composite is being disposed.
    /// </summary>
    public event EventHandler<IDirectDrawable>? Disposing;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectComposite"/> class.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host that owns the composite.</param>
    /// <param name="mode">The coordinate mode used by the composite and all of its children.</param>
    /// <param name="anchor">The composite anchor, in mode-appropriate pixels.</param>
    /// <param name="nickname">An optional diagnostic nickname.</param>
    public DirectComposite(
        RenderSurfaceHostBase renderSurfaceHost,
        DirectDrawingMode mode,
        PointF anchor = default,
        string? nickname = null)
    {
        RenderSurfaceHost =
            renderSurfaceHost ??
            throw new ArgumentNullException(nameof(renderSurfaceHost));

        Mode = mode;
        _anchor = anchor;
        Nickname = nickname ?? Id.ToString();

        _readOnlyChildren = new ReadOnlyCollection<IDirectDrawable>(_children);

        Movement = new MovementController(
            this,
            MovementState.ForPixel());

        DirectDrawingManager.Instance.AddOrReplace(this);
    }

    /// <summary>
    /// Gets the unique identifier of the composite.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the optional diagnostic nickname of the composite.
    /// </summary>
    public string? Nickname { get; private set; }

    /// <summary>
    /// Gets the render surface host shared by the composite and its children.
    /// </summary>
    public RenderSurfaceHostBase RenderSurfaceHost { get; }

    /// <summary>
    /// Gets the coordinate mode shared by the composite and its children.
    /// </summary>
    public DirectDrawingMode Mode { get; }

    /// <summary>
    /// Gets the scene layer shared by all children in scene-layer mode.
    /// </summary>
    /// <remarks>
    /// The first child establishes this value. It is cleared when the composite
    /// becomes empty.
    /// </remarks>
    public SceneLayer? SceneLayer { get; private set; }

    /// <summary>
    /// Gets the view shared by all children in view mode.
    /// </summary>
    /// <remarks>
    /// The first child establishes this value. It is cleared when the composite
    /// becomes empty.
    /// </remarks>
    public View? View { get; private set; }

    /// <summary>
    /// Gets the children owned by the composite.
    /// </summary>
    public ReadOnlyCollection<IDirectDrawable> Children => _readOnlyChildren;

    /// <summary>
    /// Gets the movement controller used to move the composite anchor.
    /// </summary>
    public MovementController Movement { get; }

    /// <summary>
    /// Gets the coordinate space used by the movement controller.
    /// </summary>
    public MovementSpace PositionSpace => MovementSpace.Pixel;

    /// <summary>
    /// Gets or sets the visibility of all descendants.
    /// </summary>
    public bool Visible
    {
        get => _children.Any(static child => child.Visible);
        set
        {
            foreach (IDirectDrawable child in _children)
                SetChildVisibility(child, value);
        }
    }

    /// <summary>
    /// Gets the composite Z-order.
    /// </summary>
    /// <remarks>
    /// A composite is not rendered directly; descendants retain their individual
    /// Z-orders. Use <see cref="SetZOrder"/> to update all descendants.
    /// </remarks>
    public int ZOrder => 0;

    /// <summary>
    /// Gets the union of all visible descendants' screen-space bounds.
    /// </summary>
    public Rectangle ScreenBounds =>
        GetBounds(static child => child.ScreenBounds);

    /// <summary>
    /// Gets the union of all visible descendants' world-space bounds.
    /// </summary>
    public Rectangle WorldBounds =>
        GetBounds(static child => child.WorldBounds);

    /// <summary>
    /// Gets the current composite anchor.
    /// </summary>
    public Vector2 GetPosition() => new(_anchor.X, _anchor.Y);

    /// <summary>
    /// Sets the composite anchor and repositions all children from their stored offsets.
    /// </summary>
    /// <param name="position">The new anchor, in mode-appropriate pixels.</param>
    public void SetPosition(Vector2 position)
    {
        SetPosition(position.X, position.Y);
    }

    /// <summary>
    /// Sets the composite anchor and repositions all children from their stored offsets.
    /// </summary>
    /// <param name="x">The new X coordinate.</param>
    /// <param name="y">The new Y coordinate.</param>
    /// <returns>The current composite.</returns>
    public DirectComposite SetPosition(float x, float y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _anchor = new PointF(x, y);
        var anchor = new Vector2(x, y);

        foreach (IDirectDrawable child in _children)
        {
            Vector2 offset =
                _localOffsetPx.TryGetValue(child, out Vector2 value)
                    ? value
                    : Vector2.Zero;

            GetMovable(child).SetPosition(anchor + offset);
        }

        return this;
    }

    /// <summary>
    /// Adds a movable direct drawable or another composite as a child.
    /// </summary>
    /// <typeparam name="TChild">
    /// A reference type implementing both <see cref="IDirectDrawable"/> and <see cref="IMovable"/>.
    /// </typeparam>
    /// <param name="child">The child to add.</param>
    /// <param name="keepCurrentOffset">
    /// <see langword="true"/> to preserve the child's current offset from the
    /// composite anchor.
    /// </param>
    /// <param name="explicitLocalOffsetPx">
    /// The offset to use when <paramref name="keepCurrentOffset"/> is
    /// <see langword="false"/>.
    /// </param>
    /// <returns>The current composite.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the child uses another render surface, mode, view, or scene
    /// layer.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the child already has another parent or would create a cycle.
    /// </exception>
    public DirectComposite Add<TChild>(
        TChild child,
        bool keepCurrentOffset = true,
        Vector2? explicitLocalOffsetPx = null)
        where TChild : class, IDirectDrawable, IMovable
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(child);

        if (_children.Contains(child))
            return this;

        ValidateChildIdentity(child);
        ValidateParentRelationship(child);
        ValidateAndAssignTarget(child);

        Vector2 offset = keepCurrentOffset
            ? child.GetPosition() - new Vector2(_anchor.X, _anchor.Y)
            : explicitLocalOffsetPx ?? Vector2.Zero;

        RegisterParent(child);

        try
        {
            _children.Add(child);
            _localOffsetPx[child] = offset;
            child.Disposing += OnChildDisposing;

            child.SetPosition(
                new Vector2(_anchor.X, _anchor.Y) + offset);
        }
        catch
        {
            child.Disposing -= OnChildDisposing;
            _children.Remove(child);
            _localOffsetPx.Remove(child);
            UnregisterParent(child);
            ResetTargetWhenEmpty();
            throw;
        }

        return this;
    }

    /// <summary>
    /// Removes a child without disposing it.
    /// </summary>
    /// <param name="child">The child to detach.</param>
    /// <returns>The current composite.</returns>
    public DirectComposite Remove(IDirectDrawable child)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(child);

        if (!_children.Remove(child))
            return this;

        child.Disposing -= OnChildDisposing;
        _localOffsetPx.Remove(child);
        UnregisterParent(child);
        ResetTargetWhenEmpty();

        return this;
    }

    /// <summary>
    /// Removes every child without disposing them.
    /// </summary>
    /// <returns>The current composite.</returns>
    public DirectComposite Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        foreach (IDirectDrawable child in _children)
        {
            child.Disposing -= OnChildDisposing;
            UnregisterParent(child);
        }

        _children.Clear();
        _localOffsetPx.Clear();
        ResetTargetWhenEmpty();

        return this;
    }

    /// <summary>
    /// Changes one child's local offset and reapplies its position.
    /// </summary>
    /// <param name="child">The child whose offset should change.</param>
    /// <param name="newLocalOffsetPx">The new offset from the composite anchor.</param>
    /// <returns>The current composite.</returns>
    public DirectComposite SetLocalOffset(
        IDirectDrawable child,
        Vector2 newLocalOffsetPx)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(child);

        if (!_children.Contains(child))
            return this;

        _localOffsetPx[child] = newLocalOffsetPx;

        GetMovable(child).SetPosition(
            new Vector2(_anchor.X, _anchor.Y) +
            newLocalOffsetPx);

        return this;
    }

    /// <summary>
    /// Applies one Z-order to every descendant.
    /// </summary>
    /// <param name="zOrder">The Z-order to apply.</param>
    /// <returns>The current composite.</returns>
    public DirectComposite SetZOrder(int zOrder)
    {
        foreach (IDirectDrawable child in _children)
            SetChildZOrder(child, zOrder);

        return this;
    }

    /// <summary>
    /// Applies one opacity to every descendant.
    /// </summary>
    /// <param name="opacity">The opacity in the range 0 through 1.</param>
    /// <returns>The current composite.</returns>
    public DirectComposite SetOpacity(float opacity)
    {
        foreach (IDirectDrawable child in _children)
            SetChildOpacity(child, opacity);

        return this;
    }

    /// <summary>
    /// Applies one visibility value to every descendant.
    /// </summary>
    /// <param name="visible">The visibility value to apply.</param>
    /// <returns>The current composite.</returns>
    public DirectComposite SetIsVisible(bool visible)
    {
        Visible = visible;
        return this;
    }

    /// <summary>
    /// Fades every descendant to the requested opacity.
    /// </summary>
    /// <param name="targetOpacity">The target opacity.</param>
    /// <param name="durationSec">The animation duration in seconds.</param>
    /// <returns>The current composite.</returns>
    public DirectComposite FadeTo(
        float targetOpacity,
        float durationSec)
    {
        foreach (IDirectDrawable child in _children)
            FadeChildTo(child, targetOpacity, durationSec);

        return this;
    }

    /// <summary>
    /// Fades every descendant to full opacity.
    /// </summary>
    /// <param name="durationSec">The animation duration in seconds.</param>
    /// <returns>The current composite.</returns>
    public DirectComposite FadeIn(float durationSec) =>
        FadeTo(1f, durationSec);

    /// <summary>
    /// Fades every descendant to zero opacity.
    /// </summary>
    /// <param name="durationSec">The animation duration in seconds.</param>
    /// <returns>The current composite.</returns>
    public DirectComposite FadeOut(float durationSec) =>
        FadeTo(0f, durationSec);

    /// <summary>
    /// Gets the union of all visible descendants as projected through a view.
    /// </summary>
    /// <param name="view">The view used for projection.</param>
    /// <returns>The projected screen-space bounds.</returns>
    public RectangleF GetDrawLocationScreen(View view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (Mode == DirectDrawingMode.View &&
            !ReferenceEquals(View, view))
        {
            return RectangleF.Empty;
        }

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (IDirectDrawable child in _children)
        {
            if (!child.Visible)
                continue;

            RectangleF bounds =
                child.GetDrawLocationScreen(view);

            if (bounds.IsEmpty)
                continue;

            minX = Math.Min(minX, bounds.Left);
            minY = Math.Min(minY, bounds.Top);
            maxX = Math.Max(maxX, bounds.Right);
            maxY = Math.Max(maxY, bounds.Bottom);
        }

        if (minX == float.MaxValue)
            return RectangleF.Empty;

        return RectangleF.FromLTRB(
            (float)Math.Floor(minX),
            (float)Math.Floor(minY),
            (float)Math.Ceiling(maxX),
            (float)Math.Ceiling(maxY));
    }

    /// <summary>
    /// Performs no direct rendering because descendants render independently.
    /// </summary>
    public void Draw(
        BackbufferBase backbuffer,
        RectangleF destRectScreen)
    {
    }

    /// <summary>
    /// Advances composite movement.
    /// </summary>
    /// <param name="tick">The current engine tick.</param>
    public void Update(long tick)
    {
        if (_disposed || tick <= _lastTick)
            return;

        Movement.AdvanceMovement(
            HighResTimer.GetDuration(_lastTick, tick));

        _lastTick = tick;
    }

    /// <summary>
    /// Disposes the composite and every child it owns.
    /// </summary>
    public virtual void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        Disposing?.Invoke(this, this);

        IDirectDrawable[] children = [.. _children];

        foreach (IDirectDrawable child in children)
            child.Dispose();

        foreach (IDirectDrawable child in _children)
        {
            child.Disposing -= OnChildDisposing;
            UnregisterParent(child);
        }

        _children.Clear();
        _localOffsetPx.Clear();
        ResetTargetWhenEmpty();
        Disposing = null;
    }

    private Rectangle GetBounds(
        Func<IDirectDrawable, Rectangle> selector)
    {
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (IDirectDrawable child in _children)
        {
            if (!child.Visible)
                continue;

            Rectangle bounds = selector(child);

            if (bounds.IsEmpty)
                continue;

            minX = Math.Min(minX, bounds.Left);
            minY = Math.Min(minY, bounds.Top);
            maxX = Math.Max(maxX, bounds.Right);
            maxY = Math.Max(maxY, bounds.Bottom);
        }

        if (minX == float.MaxValue)
            return Rectangle.Empty;

        return Rectangle.FromLTRB(
            (int)Math.Floor(minX),
            (int)Math.Floor(minY),
            (int)Math.Ceiling(maxX),
            (int)Math.Ceiling(maxY));
    }

    private void ValidateChildIdentity(IDirectDrawable child)
    {
        if (!ReferenceEquals(
                child.RenderSurfaceHost,
                RenderSurfaceHost))
        {
            throw new ArgumentException(
                "The child must belong to the same RenderSurfaceHost as the composite.",
                nameof(child));
        }

        if (child.Mode != Mode)
        {
            throw new ArgumentException(
                $"The child drawing mode '{child.Mode}' does not match " +
                $"the composite drawing mode '{Mode}'.",
                nameof(child));
        }
    }

    private void ValidateAndAssignTarget(IDirectDrawable child)
    {
        if (Mode == DirectDrawingMode.SceneLayer)
        {
            SceneLayer childLayer =
                GetSceneLayer(child) ??
                throw new ArgumentException(
                    "A scene-layer child must reference a SceneLayer.",
                    nameof(child));

            if (SceneLayer is null)
            {
                SceneLayer = childLayer;
            }
            else if (!ReferenceEquals(
                         SceneLayer,
                         childLayer))
            {
                throw new ArgumentException(
                    "All scene-layer children in a composite must reference the same SceneLayer.",
                    nameof(child));
            }

            return;
        }

        View childView =
            GetView(child) ??
            throw new ArgumentException(
                "A view child must reference a View.",
                nameof(child));

        if (View is null)
        {
            View = childView;
        }
        else if (!ReferenceEquals(
                     View,
                     childView))
        {
            throw new ArgumentException(
                "All view children in a composite must reference the same View.",
                nameof(child));
        }
    }

    private void ValidateParentRelationship(IDirectDrawable child)
    {
        lock (_parentSyncRoot)
        {
            if (_parents.TryGetValue(
                    child,
                    out DirectComposite? existingParent))
            {
                throw new InvalidOperationException(
                    $"The child already belongs to composite '{existingParent.Nickname}'.");
            }

            if (child is not DirectComposite childComposite)
                return;

            DirectComposite? ancestor = this;

            while (ancestor is not null)
            {
                if (ReferenceEquals(
                        ancestor,
                        childComposite))
                {
                    throw new InvalidOperationException(
                        "Adding the child would create a recursive composite cycle.");
                }

                _parents.TryGetValue(
                    ancestor,
                    out ancestor);
            }
        }
    }

    private void RegisterParent(IDirectDrawable child)
    {
        lock (_parentSyncRoot)
            _parents.Add(child, this);
    }

    private void UnregisterParent(IDirectDrawable child)
    {
        lock (_parentSyncRoot)
        {
            if (_parents.TryGetValue(
                    child,
                    out DirectComposite? parent) &&
                ReferenceEquals(parent, this))
            {
                _parents.Remove(child);
            }
        }
    }

    private void ResetTargetWhenEmpty()
    {
        if (_children.Count != 0)
            return;

        SceneLayer = null;
        View = null;
    }

    private void OnChildDisposing(
        object? sender,
        IDirectDrawable child)
    {
        child.Disposing -= OnChildDisposing;
        _children.Remove(child);
        _localOffsetPx.Remove(child);
        UnregisterParent(child);
        ResetTargetWhenEmpty();
    }

    private static IMovable GetMovable(IDirectDrawable child)
    {
        return child as IMovable ??
               throw new InvalidOperationException(
                   $"Composite child '{child.Nickname}' no longer implements IMovable.");
    }

    private static SceneLayer? GetSceneLayer(IDirectDrawable child) =>
        child switch
        {
            DirectDrawingBase drawing => drawing.SceneLayer,
            DirectComposite composite => composite.SceneLayer,
            _ => null
        };

    private static View? GetView(IDirectDrawable child) =>
        child switch
        {
            DirectDrawingBase drawing => drawing.View,
            DirectComposite composite => composite.View,
            _ => null
        };

    private static void SetChildVisibility(
        IDirectDrawable child,
        bool visible)
    {
        switch (child)
        {
            case DirectDrawingBase drawing:
                drawing.Visible = visible;
                break;

            case DirectComposite composite:
                composite.Visible = visible;
                break;

            default:
                throw UnsupportedChildType(child);
        }
    }

    private static void SetChildZOrder(
        IDirectDrawable child,
        int zOrder)
    {
        switch (child)
        {
            case DirectDrawingBase drawing:
                drawing.ZOrder = zOrder;
                break;

            case DirectComposite composite:
                composite.SetZOrder(zOrder);
                break;

            default:
                throw UnsupportedChildType(child);
        }
    }

    private static void SetChildOpacity(
        IDirectDrawable child,
        float opacity)
    {
        switch (child)
        {
            case DirectDrawingBase drawing:
                drawing.Opacity = opacity;
                break;

            case DirectComposite composite:
                composite.SetOpacity(opacity);
                break;

            default:
                throw UnsupportedChildType(child);
        }
    }

    private static void FadeChildTo(
        IDirectDrawable child,
        float targetOpacity,
        float durationSec)
    {
        switch (child)
        {
            case DirectDrawingBase drawing:
                drawing.FadeTo(
                    targetOpacity,
                    durationSec);
                break;

            case DirectComposite composite:
                composite.FadeTo(
                    targetOpacity,
                    durationSec);
                break;

            default:
                throw UnsupportedChildType(child);
        }
    }

    private static InvalidOperationException UnsupportedChildType(
        IDirectDrawable child)
    {
        return new InvalidOperationException(
            $"Composite child type '{child.GetType().FullName}' does not support recursive visual operations.");
    }
}
