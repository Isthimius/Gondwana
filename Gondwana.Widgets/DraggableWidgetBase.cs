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

    public event Action<WidgetDragEventArgs>? DragStarted;
    public event Action<WidgetDragEventArgs>? Dragged;
    public event Action<WidgetDragEventArgs>? DragEnded;

    #endregion Events

    #region Constructor

    protected DraggableWidgetBase(RenderSurfaceHostBase renderSurfaceHost,
                                  DirectDrawingMode mode,
                                  PointF anchor = default,
                                  string? nickname = null)
        : base(renderSurfaceHost, mode, anchor, nickname)
    {
    }

    #endregion Constructor

    #region Drag Dispatch

    protected void DispatchDragStarted(WidgetDragEventArgs args)
    {
        OnDragStarted(args);
        DragStarted?.Invoke(args);
    }

    protected void DispatchDragged(WidgetDragEventArgs args)
    {
        OnDragged(args);
        Dragged?.Invoke(args);
    }

    protected void DispatchDragEnded(WidgetDragEventArgs args)
    {
        OnDragEnded(args);
        DragEnded?.Invoke(args);
    }

    #endregion Drag Dispatch

    #region Protected Drag Hooks

    protected virtual void OnDragStarted(WidgetDragEventArgs args)
    {
    }

    protected virtual void OnDragged(WidgetDragEventArgs args)
    {
    }

    protected virtual void OnDragEnded(WidgetDragEventArgs args)
    {
    }

    #endregion Protected Drag Hooks
}