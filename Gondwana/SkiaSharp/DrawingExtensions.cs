using System.Drawing;

namespace Gondwana.SkiaSharp;

/// <summary>
/// Provides extension methods for System.Drawing types.
/// </summary>
public static class DrawingExtensions
{
    /// <summary>
    /// Ensures that the given RectangleF is aligned to pixel boundaries,
    /// so partial pixels are included in result.
    /// </summary>
    /// <param name="rectF">The rectangle with floating-point coordinates to align to pixel boundaries.</param>
    /// <returns>A rectangle with integer coordinates that fully encompasses the original rectangle.</returns>
    public static Rectangle ToPixelAlignedRect(this RectangleF rectF)
    {
        if (rectF.IsEmpty)
            return Rectangle.Empty;

        int x = (int)Math.Floor(rectF.X);
        int y = (int)Math.Floor(rectF.Y);
        int right = (int)Math.Ceiling(rectF.Right);
        int bottom = (int)Math.Ceiling(rectF.Bottom);

        return Rectangle.FromLTRB(x, y, right, bottom);
    }
}
