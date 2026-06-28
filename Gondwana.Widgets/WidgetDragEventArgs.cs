using System.Drawing;
using System.Numerics;

namespace Gondwana.Widgets;

/// <summary>
/// Event arguments for widget drag callbacks.
/// </summary>
public sealed class WidgetDragEventArgs : WidgetEventArgs
{
    public WidgetDragEventArgs(
        WidgetBase widget,
        PointF startScreenPositionPx,
        PointF currentScreenPositionPx,
        WidgetPointerButton button = WidgetPointerButton.Left,
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
    public WidgetPointerButton Button { get; }

    /// <summary>
    /// Gets the total drag offset from the drag start position.
    /// </summary>
    public Vector2 TotalDeltaPx => new(
        CurrentScreenPositionPx.X - StartScreenPositionPx.X,
        CurrentScreenPositionPx.Y - StartScreenPositionPx.Y);
}