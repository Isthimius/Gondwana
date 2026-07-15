using System.Drawing;
using System.Numerics;

namespace Gondwana.Widgets;

/// <summary>
/// Event arguments for pointer-based widget callbacks.
/// </summary>
public sealed class WidgetPointerEventArgs : WidgetEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WidgetPointerEventArgs"/> class.
    /// </summary>
    /// <param name="widget">The widget that raised the event.</param>
    /// <param name="screenPositionPx">The pointer position in screen pixels.</param>
    /// <param name="button">The pointer button involved in the interaction. Default is <see cref="WidgetPointerButtonEnum.None"/>.</param>
    /// <param name="clickCount">The number of clicks associated with this pointer action. Default is 0.</param>
    /// <param name="deltaPx">The movement delta in pixels since the previous pointer update. Default is zero vector.</param>
    /// <param name="tick">The engine or timer tick associated with the event. Default is 0.</param>
    public WidgetPointerEventArgs(
        WidgetBase widget,
        PointF screenPositionPx,
        WidgetPointerButtonEnum button = WidgetPointerButtonEnum.None,
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
    public WidgetPointerButtonEnum Button { get; }

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
    public bool IsPrimaryButton => Button is WidgetPointerButtonEnum.Left or WidgetPointerButtonEnum.Touch;
}