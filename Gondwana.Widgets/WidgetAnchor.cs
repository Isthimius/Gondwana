using System.Drawing;

namespace Gondwana.Widgets;

/// <summary>
/// Identifies one of the nine common anchor points on a rectangle.
/// </summary>
public enum WidgetAnchor
{
    /// <summary>The upper-left corner.</summary>
    TopLeft,

    /// <summary>The center of the upper edge.</summary>
    TopCenter,

    /// <summary>The upper-right corner.</summary>
    TopRight,

    /// <summary>The center of the left edge.</summary>
    CenterLeft,

    /// <summary>The center of the rectangle.</summary>
    Center,

    /// <summary>The center of the right edge.</summary>
    CenterRight,

    /// <summary>The lower-left corner.</summary>
    BottomLeft,

    /// <summary>The center of the lower edge.</summary>
    BottomCenter,

    /// <summary>The lower-right corner.</summary>
    BottomRight
}

internal static class WidgetAnchorExtensions
{
    internal static PointF GetPoint(
        this WidgetAnchor anchor,
        Rectangle bounds)
    {
        float centerX = bounds.Left + bounds.Width / 2f;
        float centerY = bounds.Top + bounds.Height / 2f;

        return anchor switch
        {
            WidgetAnchor.TopLeft => new PointF(bounds.Left, bounds.Top),
            WidgetAnchor.TopCenter => new PointF(centerX, bounds.Top),
            WidgetAnchor.TopRight => new PointF(bounds.Right, bounds.Top),
            WidgetAnchor.CenterLeft => new PointF(bounds.Left, centerY),
            WidgetAnchor.Center => new PointF(centerX, centerY),
            WidgetAnchor.CenterRight => new PointF(bounds.Right, centerY),
            WidgetAnchor.BottomLeft => new PointF(bounds.Left, bounds.Bottom),
            WidgetAnchor.BottomCenter => new PointF(centerX, bounds.Bottom),
            WidgetAnchor.BottomRight => new PointF(bounds.Right, bounds.Bottom),
            _ => throw new ArgumentOutOfRangeException(nameof(anchor), anchor, null)
        };
    }
}
