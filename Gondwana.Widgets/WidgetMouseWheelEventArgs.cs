using System.Drawing;
using Gondwana.Rendering.Views;

namespace Gondwana.Widgets;

/// <summary>
/// Event arguments for mouse-wheel input routed to a widget.
/// </summary>
public sealed class WidgetMouseWheelEventArgs : WidgetEventArgs
{
    /// <summary>Initializes a new wheel event.</summary>
    public WidgetMouseWheelEventArgs(WidgetBase widget, View view, PointF screenPositionPx, int delta, long tick = 0)
        : base(widget, tick)
    {
        View = view ?? throw new ArgumentNullException(nameof(view));
        ScreenPositionPx = screenPositionPx;
        Delta = delta;
    }

    /// <summary>Gets the view through which the input was routed.</summary>
    public View View { get; }

    /// <summary>Gets the pointer position in screen pixels.</summary>
    public PointF ScreenPositionPx { get; }

    /// <summary>Gets the wheel delta. Positive values scroll upward; negative values scroll downward.</summary>
    public int Delta { get; }
}
