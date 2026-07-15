using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using System.Drawing;

namespace Gondwana.Widgets;

/// <summary>
/// Base type for widgets that support dragging.
/// </summary>
public abstract class DraggableWidgetBase : WidgetBase
{
    #region Events

    /// <summary>
    /// Raised when a drag operation starts.
    /// </summary>
    public event Action<WidgetDragEventArgs>? DragStarted;

    /// <summary>
    /// Raised during a drag operation as the pointer moves.
    /// </summary>
    public event Action<WidgetDragEventArgs>? Dragged;

    /// <summary>
    /// Raised when a drag operation ends.
    /// </summary>
    public event Action<WidgetDragEventArgs>? DragEnded;

    #endregion Events

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="DraggableWidgetBase"/> class.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host for drawing operations.</param>
    /// <param name="mode">The drawing mode (world or screen space).</param>
    /// <param name="anchor">The anchor point for the widget in pixels. Default is (0, 0).</param>
    /// <param name="nickname">Optional friendly name for the widget.</param>
    protected DraggableWidgetBase(RenderSurfaceHostBase renderSurfaceHost,
                                  DirectDrawingMode mode,
                                  PointF anchor = default,
                                  string? nickname = null)
        : base(renderSurfaceHost, mode, anchor, nickname)
    {
    }

    #endregion Constructor

    #region Drag Dispatch

    /// <summary>
    /// Dispatches a drag started event, calling the virtual hook and raising the public event.
    /// </summary>
    /// <param name="args">The drag event arguments.</param>
    protected void DispatchDragStarted(WidgetDragEventArgs args)
    {
        OnDragStarted(args);
        DragStarted?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a dragged event, calling the virtual hook and raising the public event.
    /// </summary>
    /// <param name="args">The drag event arguments.</param>
    protected void DispatchDragged(WidgetDragEventArgs args)
    {
        OnDragged(args);
        Dragged?.Invoke(args);
    }

    /// <summary>
    /// Dispatches a drag ended event, calling the virtual hook and raising the public event.
    /// </summary>
    /// <param name="args">The drag event arguments.</param>
    protected void DispatchDragEnded(WidgetDragEventArgs args)
    {
        OnDragEnded(args);
        DragEnded?.Invoke(args);
    }

    #endregion Drag Dispatch

    #region Protected Drag Hooks

    /// <summary>
    /// Called when a drag operation starts. Override to customize behavior.
    /// </summary>
    /// <param name="args">The drag event arguments.</param>
    protected virtual void OnDragStarted(WidgetDragEventArgs args)
    {
    }

    /// <summary>
    /// Called during a drag operation as the pointer moves. Override to customize behavior.
    /// </summary>
    /// <param name="args">The drag event arguments.</param>
    protected virtual void OnDragged(WidgetDragEventArgs args)
    {
    }

    /// <summary>
    /// Called when a drag operation ends. Override to customize behavior.
    /// </summary>
    /// <param name="args">The drag event arguments.</param>
    protected virtual void OnDragEnded(WidgetDragEventArgs args)
    {
    }

    #endregion Protected Drag Hooks
}