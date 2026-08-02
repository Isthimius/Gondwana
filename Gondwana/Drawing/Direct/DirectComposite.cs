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
public class DirectComposite : IDirectCompositeChild, IDirectCompositeContainer
{
    private static readonly object _parentSyncRoot = new();

    private static readonly Dictionary<IDirectCompositeChild, DirectComposite> _parents =
        new(ReferenceEqualityComparer.Instance);

    private readonly List<IDirectCompositeChild> _children = [];
    private readonly Dictionary<IDirectCompositeChild, Vector2> _localOffsetPx =
        new(ReferenceEqualityComparer.Instance);

    private readonly ReadOnlyCollection<IDirectCompositeChild> _readOnlyChildren;

    private PointF _anchor;
    private long _lastTick = HighResTimer.GetCurrentTick();
    private bool _disposed;

    #region events

    /// <summary>
    /// Occurs when the composite is being disposed.
    /// </summary>
    public event EventHandler<IDirectDrawable>? Disposing;

    #endregion events

    #region constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectComposite"/> class.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host that owns the composite.</param>
    /// <param name="mode">The coordinate mode used by the composite and all of its children.</param>
    /// <param name="anchor">The composite anchor, in mode-appropriate pixels.</param>
    /// <param name="nickname">An optional diagnostic nickname.</param>
    public DirectComposite(RenderSurfaceHostBase renderSurfaceHost,
                           DirectDrawingMode mode,
                           PointF anchor = default,
                           string? nickname = null)
    {
        RenderSurfaceHost = renderSurfaceHost ?? throw new ArgumentNullException(nameof(renderSurfaceHost));

        Mode = mode;
        _anchor = anchor;
        Nickname = nickname ?? Id.ToString();

        _readOnlyChildren = new ReadOnlyCollection<IDirectCompositeChild>(_children);

        Movement = new MovementController(this, MovementState.ForPixel());

        DirectDrawingManager.Instance.AddOrReplace(this);
    }

    #endregion constructors

    #region public properties

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
    /// <remarks>
    /// Implements <see cref="IDirectCompositeContainer.Children"/>.
    /// </remarks>
    public ReadOnlyCollection<IDirectCompositeChild> Children => _readOnlyChildren;

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
            foreach (IDirectCompositeChild child in _children)
                child.SetIsVisible(value);
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
    public Rectangle ScreenBounds => GetBounds(static child => child.ScreenBounds);

    /// <summary>
    /// Gets the union of all visible descendants' world-space bounds.
    /// </summary>
    public Rectangle WorldBounds => GetBounds(static child => child.WorldBounds);
    
    #endregion public properties

    #region public methods

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

        foreach (IDirectCompositeChild child in _children)
        {
            Vector2 offset = _localOffsetPx.TryGetValue(child, out Vector2 value) ? value : Vector2.Zero;
            child.SetPosition(anchor + offset);
        }

        return this;
    }

    /// <summary>
    /// Adds a composite-compatible direct drawable as a child.
    /// </summary>
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
    public virtual DirectComposite Add(IDirectCompositeChild child,
                                       bool keepCurrentOffset = true,
                                       Vector2? explicitLocalOffsetPx = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(child);

        if (_children.Contains(child))
            return this;

        ValidateChildIdentity(child);
        ValidateParentRelationship(child);
        ValidateAndAssignTarget(child);

        Vector2 offset = keepCurrentOffset ? child.GetPosition() - new Vector2(_anchor.X, _anchor.Y) : explicitLocalOffsetPx ?? Vector2.Zero;

        RegisterParent(child);

        try
        {
            _children.Add(child);
            _localOffsetPx[child] = offset;
            child.Disposing += OnChildDisposing;

            child.SetPosition(new Vector2(_anchor.X, _anchor.Y) + offset);

            OnChildAdded(child);
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
    public virtual DirectComposite Remove(IDirectCompositeChild child)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(child);

        if (!_children.Remove(child))
            return this;

        child.Disposing -= OnChildDisposing;
        _localOffsetPx.Remove(child);
        UnregisterParent(child);
        ResetTargetWhenEmpty();

        OnChildRemoved(child);

        return this;
    }

    /// <summary>
    /// Removes every child without disposing them.
    /// </summary>
    /// <returns>The current composite.</returns>
    public virtual DirectComposite Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IDirectCompositeChild[] children = [.. _children];

        foreach (IDirectCompositeChild child in children)
        {
            child.Disposing -= OnChildDisposing;
            UnregisterParent(child);
        }

        _children.Clear();
        _localOffsetPx.Clear();
        ResetTargetWhenEmpty();

        foreach (IDirectCompositeChild child in children)
            OnChildRemoved(child);

        return this;
    }

    /// <summary>
    /// Changes one child's local offset and reapplies its position.
    /// </summary>
    /// <param name="child">The child whose offset should change.</param>
    /// <param name="newLocalOffsetPx">The new offset from the composite anchor.</param>
    /// <returns>The current composite.</returns>
    public DirectComposite SetLocalOffset(IDirectCompositeChild child,
                                          Vector2 newLocalOffsetPx)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(child);

        if (!_children.Contains(child))
            return this;

        _localOffsetPx[child] = newLocalOffsetPx;

        child.SetPosition(new Vector2(_anchor.X, _anchor.Y) + newLocalOffsetPx);

        return this;
    }

    /// <summary>
    /// Applies one Z-order to every descendant.
    /// </summary>
    /// <param name="zOrder">The Z-order to apply.</param>
    /// <returns>The current composite.</returns>
    public DirectComposite SetZOrder(int zOrder)
    {
        foreach (IDirectCompositeChild child in _children)
            child.SetZOrder(zOrder);

        return this;
    }

    /// <summary>
    /// Applies one opacity to every descendant.
    /// </summary>
    /// <param name="opacity">The opacity in the range 0 through 1.</param>
    /// <returns>The current composite.</returns>
    public DirectComposite SetOpacity(float opacity)
    {
        foreach (IDirectCompositeChild child in _children)
            child.SetOpacity(opacity);

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
    public DirectComposite FadeTo(float targetOpacity, float durationSec)
    {
        foreach (IDirectCompositeChild child in _children)
            child.FadeTo(targetOpacity, durationSec);

        return this;
    }

    /// <summary>
    /// Fades every descendant to full opacity.
    /// </summary>
    /// <param name="durationSec">The animation duration in seconds.</param>
    /// <returns>The current composite.</returns>
    public DirectComposite FadeIn(float durationSec) => FadeTo(1f, durationSec);

    /// <summary>
    /// Fades every descendant to zero opacity.
    /// </summary>
    /// <param name="durationSec">The animation duration in seconds.</param>
    /// <returns>The current composite.</returns>
    public DirectComposite FadeOut(float durationSec) => FadeTo(0f, durationSec);

    /// <summary>
    /// Gets the union of all visible descendants as projected through a view.
    /// </summary>
    /// <param name="view">The view used for projection.</param>
    /// <returns>The projected screen-space bounds.</returns>
    public RectangleF GetDrawLocationScreen(View view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (Mode == DirectDrawingMode.View && !ReferenceEquals(View, view))
        {
            return RectangleF.Empty;
        }

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (IDirectCompositeChild child in _children)
        {
            if (!child.Visible)
                continue;

            RectangleF bounds = child.GetDrawLocationScreen(view);

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
    /// <param name="backbuffer">The backbuffer to draw to (not used).</param>
    /// <param name="destRectScreen">The destination rectangle in screen coordinates (not used).</param>
    public void Draw(BackbufferBase backbuffer, RectangleF destRectScreen) { }

    /// <summary>
    /// Advances composite movement.
    /// </summary>
    /// <param name="tick">The current engine tick.</param>
    /// <remarks>
    /// This method uses the tick to calculate elapsed time and advance the movement controller.
    /// If the tick is less than or equal to the last processed tick, no update occurs.
    /// </remarks>
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

        IDirectCompositeChild[] children = [.. _children];

        foreach (IDirectCompositeChild child in children)
            child.Dispose();

        foreach (IDirectCompositeChild child in _children)
        {
            child.Disposing -= OnChildDisposing;
            UnregisterParent(child);
        }

        _children.Clear();
        _localOffsetPx.Clear();
        ResetTargetWhenEmpty();
        Disposing = null;
    }

    #endregion public methods

    #region IDirectCompositeChild explicit interface implementations

    void IDirectCompositeChild.SetIsVisible(bool visible) => SetIsVisible(visible);

    void IDirectCompositeChild.SetZOrder(int zOrder) => SetZOrder(zOrder);

    void IDirectCompositeChild.SetOpacity(float opacity) => SetOpacity(opacity);

    void IDirectCompositeChild.FadeTo(float targetOpacity, float durationSec) => FadeTo(targetOpacity, durationSec);

    #endregion IDirectCompositeChild explicit interface implementations

    #region IDirectCompositeContainer explicit interface implementations

    IReadOnlyCollection<IDirectCompositeChild> IDirectCompositeContainer.Children => Children;

    IDirectCompositeContainer IDirectCompositeContainer.Add(IDirectCompositeChild child, bool keepCurrentOffset, Vector2? explicitLocalOffsetPx)
    {
        return Add(child, keepCurrentOffset, explicitLocalOffsetPx);
    }

    IDirectCompositeContainer IDirectCompositeContainer.Remove(IDirectCompositeChild child)
    {
        return Remove(child);
    }

    IDirectCompositeContainer IDirectCompositeContainer.Clear()
    {
        return Clear();
    }

    #endregion IDirectCompositeContainer explicit interface implementations

    #region protected hooks

    /// <summary>
    /// Called after a child has been successfully added.
    /// </summary>
    /// <param name="child">The child that was added.</param>
    /// <remarks>
    /// Overrides should perform only non-throwing bookkeeping. Derived types that
    /// need additional validation should override <see cref="Add"/>, perform that
    /// validation before calling the base implementation, and then allow this
    /// callback to perform the resulting bookkeeping.
    /// </remarks>
    protected virtual void OnChildAdded(IDirectCompositeChild child)
    {
    }

    /// <summary>
    /// Called after a child has been removed from the composite.
    /// </summary>
    /// <param name="child">The child that was removed.</param>
    protected virtual void OnChildRemoved(IDirectCompositeChild child)
    {
    }

    #endregion protected hooks

    #region private methods

    private Rectangle GetBounds(Func<IDirectCompositeChild, Rectangle> selector)
    {
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (IDirectCompositeChild child in _children)
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

    private void ValidateChildIdentity(IDirectCompositeChild child)
    {
        if (!ReferenceEquals(child.RenderSurfaceHost, RenderSurfaceHost))
        {
            throw new ArgumentException("The child must belong to the same RenderSurfaceHost as the composite.", nameof(child));
        }

        if (child.Mode != Mode)
        {
            throw new ArgumentException($"The child drawing mode '{child.Mode}' does not match the composite drawing mode '{Mode}'.", nameof(child));
        }
    }

    private void ValidateAndAssignTarget(IDirectCompositeChild child)
    {
        if (Mode == DirectDrawingMode.SceneLayer)
        {
            SceneLayer childLayer = child.SceneLayer ?? throw new ArgumentException("A scene-layer child must reference a SceneLayer.", nameof(child));

            if (SceneLayer is null)
            {
                SceneLayer = childLayer;
            }
            else if (!ReferenceEquals(SceneLayer, childLayer))
            {
                throw new ArgumentException("All scene-layer children in a composite must reference the same SceneLayer.", nameof(child));
            }

            return;
        }

        if (child.View is null)
        {
            if (child is DirectComposite compositeChild &&
                compositeChild.Children.Count == 0)
            {
                throw new ArgumentException(
                    "An empty composite cannot be added in view mode because it has no View yet. Add at least one descendant first.",
                    nameof(child));
            }

            throw new ArgumentException("A view child must reference a View.", nameof(child));
        }

        View childView = child.View;
        if (View is null)
        {
            View = childView;
        }
        else if (!ReferenceEquals(View, childView))
        {
            throw new ArgumentException("All view children in a composite must reference the same View.", nameof(child));
        }
    }

    private void ValidateParentRelationship(IDirectCompositeChild child)
    {
        lock (_parentSyncRoot)
        {
            if (_parents.TryGetValue(child, out DirectComposite? existingParent))
            {
                throw new InvalidOperationException($"The child already belongs to composite '{existingParent.Nickname}'.");
            }

            if (child is not DirectComposite childComposite)
                return;

            DirectComposite? ancestor = this;

            while (ancestor is not null)
            {
                if (ReferenceEquals(ancestor, childComposite))
                {
                    throw new InvalidOperationException("Adding the child would create a recursive composite cycle.");
                }

                _parents.TryGetValue(ancestor, out ancestor);
            }
        }
    }

    private void RegisterParent(IDirectCompositeChild child)
    {
        lock (_parentSyncRoot)
            _parents.Add(child, this);
    }

    private void UnregisterParent(IDirectCompositeChild child)
    {
        lock (_parentSyncRoot)
        {
            if (_parents.TryGetValue(child, out DirectComposite? parent) &&
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

    private void OnChildDisposing(object? sender, IDirectDrawable drawing)
    {
        if (drawing is not IDirectCompositeChild child)
            return;

        child.Disposing -= OnChildDisposing;

        if (!_children.Remove(child))
            return;

        _localOffsetPx.Remove(child);
        UnregisterParent(child);
        ResetTargetWhenEmpty();

        OnChildRemoved(child);
    }

    #endregion private methods
}
