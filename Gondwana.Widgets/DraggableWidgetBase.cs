using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using System.Drawing;
using System.Numerics;

namespace Gondwana.Widgets;

/// <summary>
/// Base type for widgets that can be repositioned through pointer dragging.
/// </summary>
/// <remarks>
/// <para>
/// Dragging begins after the primary pointer has moved at least <see cref="DragThresholdPx"/>
/// pixels from its pointer-down position. The widget is repositioned automatically by changing
/// the anchor inherited from <see cref="DirectComposite"/>.
/// </para>
/// <para>
/// The widget input layer must dispatch pointer-move and pointer-up events to this widget after
/// pointer-down, including while the pointer is outside the widget's current bounds. In other
/// words, the input layer must provide pointer capture.
/// </para>
/// </remarks>
public abstract class DraggableWidgetBase : WidgetBase
{
    #region Fields

    private bool _isPointerDown;
    private bool _suppressNextPointerClick;
    private float _dragThresholdPx = 3f;
    private PointF _dragStartScreenPositionPx;
    private Vector2 _dragStartWidgetPositionPx;
    private WidgetPointerButtonEnum _dragButton = WidgetPointerButtonEnum.None;
    private int _dragPointerId;
    private View? _dragView;
    private Vector2 _dragStartPointerPositionPx;

    #endregion Fields

    #region Events

    /// <summary>
    /// Raised when pointer movement first crosses <see cref="DragThresholdPx"/>.
    /// </summary>
    public event Action<WidgetDragEventArgs>? DragStarted;

    /// <summary>
    /// Raised after the widget is repositioned during an active drag operation.
    /// </summary>
    public event Action<WidgetDragEventArgs>? Dragged;

    /// <summary>
    /// Raised when the pointer button that began an active drag is released.
    /// </summary>
    public event Action<WidgetDragEventArgs>? DragEnded;

    #endregion Events

    #region Properties

    /// <summary>
    /// Gets or sets whether new drag operations may begin.
    /// </summary>
    /// <remarks>
    /// Changing this property to <see langword="false"/> does not cancel a drag that is already active.
    /// </remarks>
    public bool IsDragEnabled { get; set; } = true;

    /// <summary>
    /// Gets whether a drag operation is currently active.
    /// </summary>
    public bool IsDragging { get; private set; }

    /// <summary>
    /// Gets or sets the minimum pointer movement, in screen pixels, required to begin dragging.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the assigned value is less than zero.
    /// </exception>
    public float DragThresholdPx
    {
        get => _dragThresholdPx;
        set
        {
            if (value < 0f)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The drag threshold cannot be negative.");

            _dragThresholdPx = value;
        }
    }

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="DraggableWidgetBase"/> class.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host for drawing operations.</param>
    /// <param name="mode">The drawing mode (world or screen space).</param>
    /// <param name="anchor">The anchor point for the widget in pixels. Default is (0, 0).</param>
    /// <param name="nickname">Optional friendly name for the widget.</param>
    protected DraggableWidgetBase(
        RenderSurfaceHostBase renderSurfaceHost,
        DirectDrawingMode mode,
        PointF anchor = default,
        string? nickname = null)
        : base(renderSurfaceHost, mode, anchor, nickname)
    {
    }

    #endregion Constructor

    #region Framework Processing

    /// <inheritdoc/>
    protected sealed override void ProcessHidden()
    {
        base.ProcessHidden();
        ResetPointerState(clearClickSuppression: true);
    }

    /// <inheritdoc/>
    protected sealed override void ProcessCancelled()
    {
        base.ProcessCancelled();
        ResetPointerState(clearClickSuppression: true);
    }

    /// <inheritdoc/>
    protected sealed override void ProcessPointerDown(WidgetPointerEventArgs args)
    {
        base.ProcessPointerDown(args);

        if (_isPointerDown || !CanStartDrag(args))
            return;

        _isPointerDown = true;
        _suppressNextPointerClick = false;
        IsDragging = false;

        _dragStartScreenPositionPx = args.ScreenPositionPx;
        _dragStartWidgetPositionPx = GetPosition();
        _dragButton = args.Button;
        _dragPointerId = args.PointerId;
        _dragView = args.View;
        _dragStartPointerPositionPx =
            GetPointerPositionInWidgetSpace(
                args.ScreenPositionPx,
                args.View);
    }

    /// <inheritdoc/>
    protected sealed override void ProcessPointerMove(WidgetPointerEventArgs args)
    {
        base.ProcessPointerMove(args);

        if (!_isPointerDown || args.PointerId != _dragPointerId)
            return;

        Vector2 totalScreenDeltaPx = GetTotalScreenDelta(args.ScreenPositionPx);

        if (!IsDragging)
        {
            float thresholdSquared = DragThresholdPx * DragThresholdPx;

            if (totalScreenDeltaPx.LengthSquared() < thresholdSquared)
                return;

            IsDragging = true;
            _suppressNextPointerClick = true;

            DispatchDragStarted(CreateDragEventArgs(args));

            if (!IsDragging)
                return;
        }

        ApplyDragPosition(
            totalScreenDeltaPx,
            _dragView ?? args.View);

        DispatchDragged(CreateDragEventArgs(args));
    }

    /// <inheritdoc/>
    protected sealed override void ProcessPointerUp(WidgetPointerEventArgs args)
    {
        base.ProcessPointerUp(args);

        if (!_isPointerDown || !IsMatchingRelease(args))
            return;

        WidgetDragEventArgs? dragArgs = null;

        if (IsDragging)
        {
            Vector2 totalScreenDeltaPx = GetTotalScreenDelta(args.ScreenPositionPx);
            ApplyDragPosition(
                totalScreenDeltaPx,
                _dragView ?? args.View);

            dragArgs = CreateDragEventArgs(args);
        }

        bool completedDrag = IsDragging;
        ResetPointerState();

        if (completedDrag && dragArgs is not null)
        {
            _suppressNextPointerClick = true;
            DispatchDragEnded(dragArgs);
        }
    }

    /// <inheritdoc/>
    protected sealed override bool ShouldDispatchPointerClick(WidgetPointerEventArgs args)
    {
        if (!base.ShouldDispatchPointerClick(args))
            return false;

        if (!_suppressNextPointerClick)
            return true;

        _suppressNextPointerClick = false;
        args.Handled = true;
        return false;
    }

    #endregion Framework Processing

    #region Protected Drag Customization

    /// <summary>
    /// Determines whether the supplied pointer-down event may begin drag tracking.
    /// </summary>
    /// <param name="args">The pointer-down event arguments.</param>
    /// <returns>
    /// <see langword="true"/> when dragging is enabled and the event represents the primary pointer button;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    protected virtual bool CanStartDrag(WidgetPointerEventArgs args)
    {
        return IsDragEnabled && args.IsPrimaryButton;
    }

    /// <summary>
    /// Converts total screen-space pointer movement into the coordinate space used by this widget.
    /// </summary>
    /// <param name="totalScreenDeltaPx">
    /// Total pointer movement, in screen pixels, from the drag-start position.
    /// </param>
    /// <param name="view">
    /// The view through which the drag began and remains captured.
    /// </param>
    /// <returns>
    /// The movement to apply to the widget anchor. View widgets return a screen-space
    /// delta; scene-layer widgets return a world-space delta.
    /// </returns>
    /// <remarks>
    /// The default implementation uses the view's authoritative screen-to-world transform
    /// for scene-layer widgets, including camera position, zoom, viewport offset, and parallax.
    /// Override this method only when a widget requires a different coordinate conversion.
    /// </remarks>
    protected virtual Vector2 ConvertScreenDeltaToPositionDelta(
        Vector2 totalScreenDeltaPx,
        View view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (Mode == DirectDrawingMode.View)
            return totalScreenDeltaPx;

        var currentScreenPositionPx = new PointF(
            _dragStartScreenPositionPx.X + totalScreenDeltaPx.X,
            _dragStartScreenPositionPx.Y + totalScreenDeltaPx.Y);

        Vector2 currentPointerPositionPx =
            GetPointerPositionInWidgetSpace(
                currentScreenPositionPx,
                view);

        return currentPointerPositionPx -
               _dragStartPointerPositionPx;
    }

    /// <summary>
    /// Constrains or adjusts a proposed widget anchor position before it is applied.
    /// </summary>
    /// <param name="proposedPositionPx">The proposed anchor position in pixels.</param>
    /// <returns>The position that should actually be applied.</returns>
    /// <remarks>
    /// Override this method to implement clamping, grid snapping, axis locking, or other constraints.
    /// </remarks>
    protected virtual Vector2 ConstrainDragPosition(Vector2 proposedPositionPx)
    {
        return proposedPositionPx;
    }

    /// <summary>
    /// Called when pointer movement first crosses <see cref="DragThresholdPx"/>.
    /// Override to customize behavior.
    /// </summary>
    /// <param name="args">The drag event arguments.</param>
    protected virtual void OnDragStarted(WidgetDragEventArgs args)
    {
    }

    /// <summary>
    /// Called after the widget is repositioned during an active drag operation.
    /// Override to customize behavior.
    /// </summary>
    /// <param name="args">The drag event arguments.</param>
    protected virtual void OnDragged(WidgetDragEventArgs args)
    {
    }

    /// <summary>
    /// Called when the pointer button that began an active drag is released.
    /// Override to customize behavior.
    /// </summary>
    /// <param name="args">The drag event arguments.</param>
    protected virtual void OnDragEnded(WidgetDragEventArgs args)
    {
    }

    #endregion Protected Drag Customization

    #region Private Methods

    private Vector2 GetPointerPositionInWidgetSpace(
        PointF screenPositionPx,
        View view)
    {
        if (Mode == DirectDrawingMode.View)
        {
            return new Vector2(
                screenPositionPx.X,
                screenPositionPx.Y);
        }

        var sceneLayer = SceneLayer
            ?? throw new InvalidOperationException(
                "A scene-layer draggable widget must contain children attached to a SceneLayer.");

        PointF worldPositionPx =
            view.ScreenPxToWorldPx(
                sceneLayer,
                screenPositionPx);

        return new Vector2(
            worldPositionPx.X,
            worldPositionPx.Y);
    }

    private Vector2 GetTotalScreenDelta(PointF currentScreenPositionPx)
    {
        return new Vector2(
            currentScreenPositionPx.X - _dragStartScreenPositionPx.X,
            currentScreenPositionPx.Y - _dragStartScreenPositionPx.Y);
    }

    private void ApplyDragPosition(
        Vector2 totalScreenDeltaPx,
        View view)
    {
        Vector2 positionDeltaPx =
            ConvertScreenDeltaToPositionDelta(
                totalScreenDeltaPx,
                view);

        Vector2 proposedPositionPx = _dragStartWidgetPositionPx + positionDeltaPx;
        Vector2 constrainedPositionPx = ConstrainDragPosition(proposedPositionPx);

        SetPosition(constrainedPositionPx);
    }

    private bool IsMatchingRelease(WidgetPointerEventArgs args)
    {
        return args.PointerId == _dragPointerId &&
               (args.Button == WidgetPointerButtonEnum.None || args.Button == _dragButton);
    }

    private WidgetDragEventArgs CreateDragEventArgs(WidgetPointerEventArgs pointerArgs)
    {
        return new WidgetDragEventArgs(
            this,
            _dragStartScreenPositionPx,
            pointerArgs.ScreenPositionPx,
            _dragButton,
            pointerArgs.Tick,
            pointerArgs.PointerId);
    }

    private void DispatchDragStarted(WidgetDragEventArgs args)
    {
        OnDragStarted(args);
        DragStarted?.Invoke(args);
    }

    private void DispatchDragged(WidgetDragEventArgs args)
    {
        OnDragged(args);
        Dragged?.Invoke(args);
    }

    private void DispatchDragEnded(WidgetDragEventArgs args)
    {
        OnDragEnded(args);
        DragEnded?.Invoke(args);
    }

    private void ResetPointerState(bool clearClickSuppression = false)
    {
        _isPointerDown = false;
        IsDragging = false;
        _dragStartScreenPositionPx = default;
        _dragStartWidgetPositionPx = default;
        _dragButton = WidgetPointerButtonEnum.None;
        _dragPointerId = default;
        _dragView = null;
        _dragStartPointerPositionPx = default;

        if (clearClickSuppression)
            _suppressNextPointerClick = false;
    }

    #endregion Private Methods
}