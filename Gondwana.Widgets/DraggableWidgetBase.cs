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

    protected DraggableWidgetBase(
        RenderSurfaceHostBase renderSurfaceHost,
        DirectDrawingMode mode,
        PointF anchor = default,
        string? nickname = null)
        : base(renderSurfaceHost, mode, anchor, nickname)
    {
    }

    #endregion Constructor

    #region Protected Event Raisers

    protected virtual void OnDragStarted(WidgetDragEventArgs args)
    {
        DragStarted?.Invoke(args);
    }

    protected virtual void OnDragged(WidgetDragEventArgs args)
    {
        Dragged?.Invoke(args);
    }

    protected virtual void OnDragEnded(WidgetDragEventArgs args)
    {
        DragEnded?.Invoke(args);
    }

    #endregion Protected Event Raisers
}