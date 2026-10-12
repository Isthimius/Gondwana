using System.Drawing;
using Gondwana.Rendering.Views;

namespace Gondwana.Widgets;

/// <summary>
/// Describes an unhandled primary pointer action routed to gameplay after widget hit testing.
/// </summary>
public sealed class PrimaryPointerActionEventArgs : EventArgs
{
    /// <summary>Creates a gameplay pointer action.</summary>
    /// <param name="view">The topmost view containing the pointer.</param>
    /// <param name="screenPositionPx">The position in logical screen pixels.</param>
    /// <param name="isTouch">True for a recognized single-finger tap; false for a left mouse press.</param>
    /// <param name="pointerId">The touch ID or <see cref="WidgetInputRouter.MousePointerId"/>.</param>
    public PrimaryPointerActionEventArgs(View view, Point screenPositionPx, bool isTouch, int pointerId)
    {
        View = view ?? throw new ArgumentNullException(nameof(view));
        ScreenPositionPx = screenPositionPx;
        IsTouch = isTouch;
        PointerId = pointerId;
    }

    /// <summary>Gets the view whose viewport contains this action.</summary>
    public View View { get; }

    /// <summary>Gets the action position in logical screen pixels.</summary>
    public Point ScreenPositionPx { get; }

    /// <summary>Gets whether this action originated from a touch tap.</summary>
    public bool IsTouch { get; }

    /// <summary>Gets the touch contact ID, or the mouse pointer ID for mouse actions.</summary>
    public int PointerId { get; }
}
