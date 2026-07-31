using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;

namespace Gondwana.Widgets;

/// <summary>
/// Base type for container widgets that can be repositioned through pointer dragging.
/// </summary>
public abstract class DraggableContainerWidget : ContainerWidget
{
    private readonly WidgetDragBehavior _dragBehavior;

    /// <summary>
    /// Raised when pointer movement first crosses <see cref="DragThresholdPx"/>.
    /// </summary>
    public event Action<WidgetDragEventArgs>? DragStarted;

    /// <summary>
    /// Raised after the container is repositioned during an active drag.
    /// </summary>
    public event Action<WidgetDragEventArgs>? Dragged;

    /// <summary>
    /// Raised when an active drag ends.
    /// </summary>
    public event Action<WidgetDragEventArgs>? DragEnded;

    /// <summary>
    /// Initializes a new instance of the <see cref="DraggableContainerWidget"/> class.
    /// </summary>
    protected DraggableContainerWidget(RenderSurfaceHostBase renderSurfaceHost,
                                       DirectDrawingMode mode,
                                       PointF anchor = default,
                                       string? nickname = null)
        : base(renderSurfaceHost, mode, anchor, nickname)
    {
        _dragBehavior = new WidgetDragBehavior(this);
    }

    /// <summary>
    /// Gets or sets whether new drag operations may begin.
    /// </summary>
    public bool IsDragEnabled
    {
        get => _dragBehavior.IsDragEnabled;
        set => _dragBehavior.IsDragEnabled = value;
    }

    /// <summary>
    /// Gets whether a drag operation is active.
    /// </summary>
    public bool IsDragging => _dragBehavior.IsDragging;

    /// <summary>
    /// Gets or sets the minimum pointer movement required to begin dragging.
    /// </summary>
    public float DragThresholdPx
    {
        get => _dragBehavior.DragThresholdPx;
        set => _dragBehavior.DragThresholdPx = value;
    }

    /// <inheritdoc/>
    protected sealed override void ProcessHidden()
    {
        base.ProcessHidden();

        _dragBehavior.ResetPointerState(clearClickSuppression: true);
    }

    /// <inheritdoc/>
    protected sealed override void ProcessCancelled()
    {
        base.ProcessCancelled();

        _dragBehavior.ResetPointerState(clearClickSuppression: true);
    }

    /// <inheritdoc/>
    protected sealed override void ProcessPointerDown(WidgetPointerEventArgs args)
    {
        base.ProcessPointerDown(args);

        _dragBehavior.ProcessPointerDown(args, CanStartDrag);
    }

    /// <inheritdoc/>
    protected sealed override void ProcessPointerMove(WidgetPointerEventArgs args)
    {
        base.ProcessPointerMove(args);

        _dragBehavior.ProcessPointerMove(args,
                                         ConvertScreenDeltaToPositionDelta,
                                         ConstrainDragPosition,
                                         DispatchDragStarted,
                                         DispatchDragged);
    }

    /// <inheritdoc/>
    protected sealed override void ProcessPointerUp(WidgetPointerEventArgs args)
    {
        base.ProcessPointerUp(args);

        _dragBehavior.ProcessPointerUp(args,
                                       ConvertScreenDeltaToPositionDelta,
                                       ConstrainDragPosition,
                                       DispatchDragEnded);
    }

    /// <inheritdoc/>
    protected sealed override bool ShouldDispatchPointerClick(WidgetPointerEventArgs args)
    {
        return base.ShouldDispatchPointerClick(args) &&
               _dragBehavior.ShouldDispatchPointerClick(args);
    }

    /// <summary>
    /// Determines whether a pointer-down event may begin drag tracking.
    /// </summary>
    protected virtual bool CanStartDrag(WidgetPointerEventArgs args)
    {
        return _dragBehavior.CanStartDrag(args);
    }

    /// <summary>
    /// Converts total screen movement into this widget's coordinate space.
    /// </summary>
    protected virtual Vector2 ConvertScreenDeltaToPositionDelta(Vector2 totalScreenDeltaPx, View view)
    {
        return _dragBehavior.ConvertScreenDeltaToPositionDelta(totalScreenDeltaPx, view);
    }

    /// <summary>
    /// Constrains a proposed anchor position before it is applied.
    /// </summary>
    protected virtual Vector2 ConstrainDragPosition(Vector2 proposedPositionPx)
    {
        return proposedPositionPx;
    }

    /// <summary>
    /// Called when dragging begins.
    /// </summary>
    protected virtual void OnDragStarted(WidgetDragEventArgs args)
    {
    }

    /// <summary>
    /// Called after the container moves during dragging.
    /// </summary>
    protected virtual void OnDragged(WidgetDragEventArgs args)
    {
    }

    /// <summary>
    /// Called when dragging ends.
    /// </summary>
    protected virtual void OnDragEnded(WidgetDragEventArgs args)
    {
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
}
