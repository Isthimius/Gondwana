using System.Drawing;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Provides utility methods for calculating tile boundaries and applying adjustments.
/// </summary>
public static class TileBounds
{
    /// <summary>
    /// Applies an overhang adjustment to a rectangle, expanding its bounds in all directions.
    /// </summary>
    /// <param name="baseRect">The base rectangle to adjust.</param>
    /// <param name="oh">The overhang values to apply to each side of the rectangle.</param>
    /// <param name="include">If <c>true</c>, applies the overhang; if <c>false</c>, returns the original rectangle.</param>
    /// <returns>
    /// A rectangle expanded by the overhang amounts if <paramref name="include"/> is <c>true</c> and the overhang is not empty;
    /// otherwise, the original <paramref name="baseRect"/>.
    /// </returns>
    public static Rectangle ApplyOverhang(Rectangle baseRect, Overhang oh, bool include)
    {
        if (!include || oh.IsEmpty)
            return baseRect;

        return Rectangle.FromLTRB(
            baseRect.Left - oh.Left,
            baseRect.Top - oh.Top,
            baseRect.Right + oh.Right,
            baseRect.Bottom + oh.Bottom
        );
    }
}