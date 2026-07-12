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
    /// <param name="button">The pointer button used for dragging. Default is <see cref="WidgetPointerButtonEnum.Left"/>.</param>
    /// <param name="tick">The engine or timer tick associated with the event. Default is 0.</param>
    public WidgetDragEventArgs(
        WidgetBase widget,
        PointF startScreenPositionPx,
        PointF currentScreenPositionPx,
        WidgetPointerButtonEnum button = WidgetPointerButtonEnum.Left,
        long tick = 0)
        : base(widget, tick)
    {
        StartScreenPositionPx = startScreenPositionPx;
        CurrentScreenPositionPx = currentScreenPositionPx;
        Button = button;
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
    /// Gets the total drag offset from the drag start position.
    /// </summary>
    public Vector2 TotalDeltaPx => new(
        CurrentScreenPositionPx.X - StartScreenPositionPx.X,
        CurrentScreenPositionPx.Y - StartScreenPositionPx.Y);
}