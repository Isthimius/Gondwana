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
/// Represents a composite container that groups multiple direct drawable items together,
/// managing their positions relative to a common anchor point and providing batch operations.
/// </summary>
public class DirectComposite : IDirectDrawable, IMovable
{
    #region Fields

    private readonly List<DirectDrawingMovableBase> _children = new();
    private readonly Dictionary<DirectDrawingMovableBase, Vector2> _localOffsetPx = new();
    private PointF _anchor;
    private long _lastTick = HighResTimer.GetCurrentTick();

    #endregion Fields

    #region Events

    /// <summary>
    /// Event raised when this composite is being disposed.
    /// </summary>
    public event EventHandler<IDirectDrawable>? Disposing;

    #endregion Events

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectComposite"/> class.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host for drawing operations.</param>
    /// <param name="mode">The drawing mode (world or screen space).</param>
    /// <param name="anchor">The anchor point for the composite in pixels. Default is (0, 0).</param>
    /// <param name="nickname">Optional friendly name for the composite.</param>
    public DirectComposite(RenderSurfaceHostBase renderSurfaceHost, DirectDrawingMode mode, PointF anchor = default, string? nickname = null)
    {
        RenderSurfaceHost = renderSurfaceHost;
        Mode = mode;
        _anchor = anchor;
        Children = new ReadOnlyCollection<DirectDrawingMovableBase>(_children);
        Nickname = nickname ?? Id.ToString();

        Movement = new MovementController(this, MovementState.ForPixel());
        DirectDrawingManager.Instance.AddOrReplace(this);
    }

    #endregion Constructor

    #region Properties

    /// <summary>
    /// Gets the render surface host associated with this composite.
    /// </summary>
    public RenderSurfaceHostBase RenderSurfaceHost { get; }

    /// <summary>
    /// Gets the drawing mode for this composite (world or screen space).
    /// </summary>
    public DirectDrawingMode Mode { get; }

    /// <summary>
    /// Gets the scene layer shared by all children when <see cref="Mode"/> is
    /// <see cref="DirectDrawingMode.SceneLayer"/>, or <see langword="null"/> when
    /// the composite has no children or uses view mode.
    /// </summary>
    public SceneLayer? SceneLayer { get; private set; }

    /// <summary>
    /// Gets the view shared by all children when <see cref="Mode"/> is
    /// <see cref="DirectDrawingMode.View"/>, or <see langword="null"/> when
    /// the composite has no children or uses scene-layer mode.
    /// </summary>
    public View? View { get; private set; }

    /// <summary>
    /// Gets the read-only collection of child drawable items in this composite.
    /// </summary>
    public ReadOnlyCollection<DirectDrawingMovableBase> Children { get; }

    /// <summary>
    /// Gets the movement controller for animating the composite's position.
    /// </summary>
    public MovementController Movement { get; private set; }

    /// <summary>
    /// Gets the bounding rectangle that encompasses all visible children in screen coordinates.
    /// </summary>
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

            if (minX == float.MaxValue)
                return Rectangle.Empty;

            return Rectangle.FromLTRB(
                (int)Math.Floor(minX),
                (int)Math.Floor(minY),
                (int)Math.Ceiling(maxX),
                (int)Math.Ceiling(maxY));
        }
    }

    /// <summary>
    /// Gets the bounding rectangle that encompasses all visible children in world coordinates.
    /// </summary>
    public Rectangle WorldBounds
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

                var b = child.WorldBounds;

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
                return Rectangle.Empty;

            return Rectangle.FromLTRB(
                (int)Math.Floor(minX),
                (int)Math.Floor(minY),
                (int)Math.Ceiling(maxX),
                (int)Math.Ceiling(maxY));
        }
    }

    #endregion Properties

    #region IMovable Implementation

    /// <summary>
    /// Gets the position space used by this composite. Always returns <see cref="MovementSpace.Pixel"/>.
    /// </summary>
    public MovementSpace PositionSpace => MovementSpace.Pixel;

    /// <summary>
    /// Gets the current anchor position of the composite in pixels.
    /// </summary>
    /// <returns>The anchor position as a Vector2.</returns>
    public Vector2 GetPosition() => new(_anchor.X, _anchor.Y);

    /// <summary>
    /// Sets the composite anchor position using a Vector2.
    /// </summary>
    /// <param name="pos">The new anchor position in pixels.</param>
    public void SetPosition(Vector2 pos) => SetPosition(pos.X, pos.Y);

    #endregion IMovable Implementation

    #region IDirectDrawable Implementation

    /// <summary>
    /// Gets the unique identifier for this composite.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the optional friendly name for this composite.
    /// </summary>
    public string? Nickname { get; private set; }

    /// <summary>
    /// Gets or sets the visibility of the composite.
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

    /// <summary>
    /// Gets the Z-order of the composite. Always returns 0 as individual children manage their own Z-order.
    /// </summary>
    public int ZOrder => 0;

    /// <summary>
    /// Gets the screen rectangle that encompasses all visible children in the composite.
    /// </summary>
    /// <param name="view">The view to use for coordinate transformation.</param>
    /// <returns>The bounding rectangle in screen coordinates, or an empty rectangle if no children are visible.</returns>
    public RectangleF GetDrawLocationScreen(View view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (_children.Count == 0)
            return RectangleF.Empty;

        if (Mode == DirectDrawingMode.View &&
            !ReferenceEquals(View, view))
        {
            return RectangleF.Empty;
        }

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var child in _children)
        {
            if (!child.Visible)
                continue;

            var b = child.GetDrawLocationScreen(view);

            if (b == RectangleF.Empty)
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
            return RectangleF.Empty;

        return RectangleF.FromLTRB(
            (int)Math.Floor(minX),
            (int)Math.Floor(minY),
            (int)Math.Ceiling(maxX),
            (int)Math.Ceiling(maxY));
    }

    /// <summary>
    /// Draws the composite. This is intentionally a no-op as children are responsible for their own rendering.
    /// </summary>
    /// <param name="backbuffer">The backbuffer to draw to.</param>
    /// <param name="destRectScreen">The destination rectangle in screen coordinates.</param>
    public void Draw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        // Intentionally no-op.
        // Composite is a grouping/controller object; children are responsible for rendering.
    }

    #endregion IDirectDrawable Implementation

    #region Collection Management

    /// <summary>
    /// Adds a child drawing and stores its local pixel offset from the composite anchor.
    /// </summary>
    /// <param name="child">The child drawing to add.</param>
    /// <param name="keepCurrentOffset">
    /// <see langword="true"/> to preserve the child's current position relative to the
    /// composite anchor; otherwise, use <paramref name="explicitLocalOffsetPx"/>.
    /// </param>
    /// <param name="explicitLocalOffsetPx">
    /// The local offset to use when <paramref name="keepCurrentOffset"/> is
    /// <see langword="false"/>.
    /// </param>
    /// <returns>This composite for method chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="child"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the child belongs to a different render surface host or uses a
    /// different drawing mode.
    /// </exception>
    public DirectComposite Add(
        DirectDrawingMovableBase child,
        bool keepCurrentOffset = true,
        Vector2? explicitLocalOffsetPx = null)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (!ReferenceEquals(child.RenderSurfaceHost, RenderSurfaceHost))
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

        ValidateAndAssignTarget(child);

        if (_children.Contains(child))
            return this;

        Vector2 offset = keepCurrentOffset
            ? child.GetPosition() - new Vector2(_anchor.X, _anchor.Y)
            : explicitLocalOffsetPx ?? Vector2.Zero;

        _children.Add(child);
        _localOffsetPx[child] = offset;
        child.Disposing += OnChildDisposing;

        child.SetPosition(
            new Vector2(_anchor.X, _anchor.Y) + offset);

        return this;
    }

    /// <summary>
    /// Removes a child from the composite.
    /// </summary>
    /// <param name="child">The child drawable to remove.</param>
    /// <returns>This composite instance for method chaining.</returns>
    public DirectComposite Remove(DirectDrawingMovableBase child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (_children.Remove(child))
        {
            child.Disposing -= OnChildDisposing;
            _localOffsetPx.Remove(child);
            ResetTargetWhenEmpty();
        }

        return this;
    }

    /// <summary>
    /// Removes all children from the composite.
    /// </summary>
    /// <returns>This composite instance for method chaining.</returns>
    public DirectComposite Clear()
    {
        foreach (var child in _children)
            child.Disposing -= OnChildDisposing;

        _children.Clear();
        _localOffsetPx.Clear();
        ResetTargetWhenEmpty();
        return this;
    }

    #endregion Collection Management

    #region Position Management

    /// <summary>
    /// Sets the composite anchor position in pixels and repositions all children from their stored local offsets.
    /// </summary>
    /// <param name="x">The X coordinate of the anchor in pixels.</param>
    /// <param name="y">The Y coordinate of the anchor in pixels.</param>
    /// <returns>This composite instance for method chaining.</returns>
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

    /// <summary>
    /// Changes a specific child's local pixel offset and re-applies its absolute position.
    /// </summary>
    /// <param name="child">The child whose offset to change.</param>
    /// <param name="newLocalOffsetPx">The new local pixel offset relative to the composite's anchor.</param>
    /// <returns>This composite instance for method chaining.</returns>
    public DirectComposite SetLocalOffset(DirectDrawingMovableBase child, Vector2 newLocalOffsetPx)
    {
        if (!_children.Contains(child))
            return this;

        _localOffsetPx[child] = newLocalOffsetPx;
        child.SetPosition(new Vector2(_anchor.X, _anchor.Y) + newLocalOffsetPx);
        return this;
    }

    #endregion Position Management

    #region Visual Properties

    /// <summary>
    /// Sets the Z-order for all children in the composite.
    /// </summary>
    /// <param name="z">The Z-order value to apply to all children.</param>
    /// <returns>This composite instance for method chaining.</returns>
    public DirectComposite SetZOrder(int z)
    {
        foreach (var c in _children)
            c.ZOrder = z;

        return this;
    }

    /// <summary>
    /// Sets the opacity for all children in the composite.
    /// </summary>
    /// <param name="opacity">The opacity value (0.0 to 1.0) to apply to all children.</param>
    /// <returns>This composite instance for method chaining.</returns>
    public DirectComposite SetOpacity(float opacity)
    {
        foreach (var c in _children)
            c.Opacity = opacity;

        return this;
    }

    /// <summary>
    /// Sets the visibility for all children in the composite.
    /// </summary>
    /// <param name="visible">True to make all children visible; false to hide them.</param>
    /// <returns>This composite instance for method chaining.</returns>
    public DirectComposite SetIsVisible(bool visible)
    {
        foreach (var c in _children)
            c.Visible = visible;

        return this;
    }

    #endregion Visual Properties

    #region Animation

    /// <summary>
    /// Fades all children to the specified target opacity over the given duration.
    /// </summary>
    /// <param name="targetOpacity">The target opacity value (0.0 to 1.0).</param>
    /// <param name="durationSec">The duration of the fade animation in seconds.</param>
    /// <returns>This composite instance for method chaining.</returns>
    public DirectComposite FadeTo(float targetOpacity, float durationSec)
    {
        foreach (var c in _children)
            c.FadeTo(targetOpacity, durationSec);

        return this;
    }

    /// <summary>
    /// Fades all children to full opacity (1.0) over the given duration.
    /// </summary>
    /// <param name="durationSec">The duration of the fade-in animation in seconds.</param>
    /// <returns>This composite instance for method chaining.</returns>
    public DirectComposite FadeIn(float durationSec)
    {
        foreach (var c in _children)
            c.FadeIn(durationSec);

        return this;
    }

    /// <summary>
    /// Fades all children to zero opacity (0.0) over the given duration.
    /// </summary>
    /// <param name="durationSec">The duration of the fade-out animation in seconds.</param>
    /// <returns>This composite instance for method chaining.</returns>
    public DirectComposite FadeOut(float durationSec)
    {
        foreach (var c in _children)
            c.FadeOut(durationSec);

        return this;
    }

    #endregion Animation

    #region Update

    /// <summary>
    /// Updates the composite's movement controller based on the current tick.
    /// </summary>
    /// <param name="tick">The current high-resolution tick value.</param>
    public void Update(long tick)
    {
        Movement.AdvanceMovement(HighResTimer.GetDuration(_lastTick, tick));
        _lastTick = tick;
    }

    #endregion Update

    #region Disposal

    /// <summary>
    /// Releases all resources used by the composite and disposes all child drawables.
    /// </summary>
    public virtual void Dispose()
    {
        Disposing?.Invoke(this, this);

        // Create a copy of the children list to avoid collection modification during enumeration
        var childrenToDispose = _children.ToList();
        foreach (var c in childrenToDispose)
            c.Dispose();

        _children.Clear();
        _localOffsetPx.Clear();
        ResetTargetWhenEmpty();
    }

    #endregion Disposal

    #region Private Methods

    private void ValidateAndAssignTarget(
        DirectDrawingMovableBase child)
    {
        if (Mode == DirectDrawingMode.SceneLayer)
        {
            SceneLayer childLayer = child.SceneLayer
                ?? throw new ArgumentException(
                    "A scene-layer child must reference a SceneLayer.",
                    nameof(child));

            if (SceneLayer is null)
            {
                SceneLayer = childLayer;
            }
            else if (!ReferenceEquals(SceneLayer, childLayer))
            {
                throw new ArgumentException(
                    "All scene-layer children in a composite must reference the same SceneLayer.",
                    nameof(child));
            }

            return;
        }

        View childView = child.View
            ?? throw new ArgumentException(
                "A view child must reference a View.",
                nameof(child));

        if (View is null)
        {
            View = childView;
        }
        else if (!ReferenceEquals(View, childView))
        {
            throw new ArgumentException(
                "All view children in a composite must reference the same View.",
                nameof(child));
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
        IDirectDrawable drawing)
    {
        if (drawing is not DirectDrawingMovableBase child)
            return;

        child.Disposing -= OnChildDisposing;
        _children.Remove(child);
        _localOffsetPx.Remove(child);
        ResetTargetWhenEmpty();
    }

    #endregion Private Methods
}