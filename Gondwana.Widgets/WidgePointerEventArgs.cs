using System.Drawing;
using System.Numerics;

namespace Gondwana.Widgets;

/// <summary>
/// Event arguments for pointer-based widget callbacks.
/// </summary>
public sealed class WidgetPointerEventArgs : WidgetEventArgs
{
    public WidgetPointerEventArgs(
        WidgetBase widget,
        PointF screenPositionPx,
        WidgetPointerButton button = WidgetPointerButton.None,
        int clickCount = 0,
        Vector2 deltaPx = default,
        long tick = 0)
        : base(widget, tick)
    {
        ScreenPositionPx = screenPositionPx;
        Button = button;
        ClickCount = clickCount;
        DeltaPx = deltaPx;
    }

    /// <summary>
    /// Gets the pointer position in screen pixels.
    /// </summary>
    public PointF ScreenPositionPx { get; }

    /// <summary>
    /// Gets the pointer button involved in the interaction.
    /// </summary>
    public WidgetPointerButton Button { get; }

    /// <summary>
    /// Gets the number of clicks associated with this pointer action.
    /// </summary>
    public int ClickCount { get; }

    /// <summary>
    /// Gets the movement delta in pixels since the previous pointer update.
    /// </summary>
    public Vector2 DeltaPx { get; }

    /// <summary>
    /// Gets whether the pointer event represents a primary-button interaction.
    /// </summary>
    public bool IsPrimaryButton => Button is WidgetPointerButton.Left or WidgetPointerButton.Touch;
}