using System.Drawing;
using System.Numerics;

namespace Gondwana.Widgets;

/// <summary>
/// Event arguments for widget drag callbacks.
/// </summary>
public sealed class WidgetDragEventArgs : WidgetEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WidgetDragEventArgs"/> class.
    /// </summary>
    /// <param name="widget">The widget that raised the event.</param>
    /// <param name="startScreenPositionPx">The screen position where the drag began.</param>
    /// <param name="currentScreenPositionPx">The current pointer position in screen pixels.</param>
    /// <param name="button">The pointer button used for dragging.</param>
    /// <param name="tick">The engine tick associated with the event.</param>
    /// <param name="pointerId">The identifier of the pointer performing the drag.</param>
    public WidgetDragEventArgs(
        WidgetBase widget,
        PointF startScreenPositionPx,
        PointF currentScreenPositionPx,
        WidgetPointerButtonEnum button = WidgetPointerButtonEnum.Left,
        long tick = 0,
        int pointerId = 0)
        : base(widget, tick)
    {
        StartScreenPositionPx = startScreenPositionPx;
        CurrentScreenPositionPx = currentScreenPositionPx;
        Button = button;
        PointerId = pointerId;
    }

    /// <summary>
    /// Gets the screen position where the drag began.
    /// </summary>
    public PointF StartScreenPositionPx { get; }

    /// <summary>
    /// Gets the current pointer position in screen pixels.
    /// </summary>
    public PointF CurrentScreenPositionPx { get; }

    /// <summary>
    /// Gets the pointer button used for dragging.
    /// </summary>
    public WidgetPointerButtonEnum Button { get; }

    /// <summary>
    /// Gets the identifier of the pointer performing the drag.
    /// </summary>
    public int PointerId { get; }

    /// <summary>
    /// Gets the total drag offset from the drag start position.
    /// </summary>
    public Vector2 TotalDeltaPx => new(
        CurrentScreenPositionPx.X - StartScreenPositionPx.X,
        CurrentScreenPositionPx.Y - StartScreenPositionPx.Y);
}
