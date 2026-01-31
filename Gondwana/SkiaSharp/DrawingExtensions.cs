using System.Drawing;

namespace Gondwana.SkiaSharp;

public static class DrawingExtensions
{
    /// <summary>
    /// Ensures that the given RectangleF is aligned to pixel boundaries,
    /// so partial pixels are included in result.
    /// </summary>
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
