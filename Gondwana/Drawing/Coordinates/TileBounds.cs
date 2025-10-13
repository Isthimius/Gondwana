using System.Drawing;

namespace Gondwana.Drawing.Coordinates;

public static class TileBounds
{
    public static Rectangle ApplyOverhang(Rectangle baseRect, Overhang oh, bool include)
    {
        if (!include || oh.IsEmpty) return baseRect;
        return Rectangle.FromLTRB(
            baseRect.Left - oh.Left,
            baseRect.Top - oh.Top,
            baseRect.Right + oh.Right,
            baseRect.Bottom + oh.Bottom
        );
    }
}