using System.Drawing;
using System.Numerics;
using Gondwana.Rendering.Views;

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
    /// <param name="view">The view through which the pointer interaction was routed.</param>
    /// <param name="screenPositionPx">The pointer position in screen pixels.</param>
    /// <param name="button">The pointer button involved in the interaction.</param>
    /// <param name="clickCount">The number of clicks associated with this pointer action.</param>
    /// <param name="deltaPx">The movement delta in pixels since the previous pointer update.</param>
    /// <param name="tick">The engine tick associated with the event.</param>
    /// <param name="pointerId">
    /// The identifier of the pointer that produced the event. Mouse input uses
    /// <see cref="WidgetInputRouter.MousePointerId"/>; touch input uses its contact ID.
    /// </param>
    public WidgetPointerEventArgs(
        WidgetBase widget,
        View view,
        PointF screenPositionPx,
        WidgetPointerButtonEnum button = WidgetPointerButtonEnum.None,
        int clickCount = 0,
        Vector2 deltaPx = default,
        long tick = 0,
        int pointerId = 0)
        : base(widget, tick)
    {
        View = view ?? throw new ArgumentNullException(nameof(view));
        ScreenPositionPx = screenPositionPx;
        Button = button;
        ClickCount = clickCount;
        DeltaPx = deltaPx;
        PointerId = pointerId;
    }

    /// <summary>
    /// Gets the view through which the pointer interaction was routed.
    /// </summary>
    public View View { get; }

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
    /// Gets the identifier of the pointer that produced the event.
    /// </summary>
    public int PointerId { get; }

    /// <summary>
    /// Gets whether the pointer event represents a primary-button interaction.
    /// </summary>
    public bool IsPrimaryButton => Button is WidgetPointerButtonEnum.Left or WidgetPointerButtonEnum.Touch;
}