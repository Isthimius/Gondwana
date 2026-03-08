namespace Gondwana.Drawing;

/// <summary>
/// Represents the overhang dimensions (in pixels) that extend beyond a tile's primary area.
/// Overhang values define how much a tile's visual representation exceeds its logical boundaries
/// in each direction (left, top, right, and bottom).
/// </summary>
/// <param name="Left">The number of pixels the tile extends beyond its left boundary.</param>
/// <param name="Top">The number of pixels the tile extends beyond its top boundary.</param>
/// <param name="Right">The number of pixels the tile extends beyond its right boundary.</param>
/// <param name="Bottom">The number of pixels the tile extends beyond its bottom boundary.</param>
public record struct Overhang(int Left, int Top, int Right, int Bottom)
{
    /// <summary>
    /// Represents an overhang with no extension in any direction (all values are zero).
    /// </summary>
    public static readonly Overhang None = new(0, 0, 0, 0);
    
    /// <summary>
    /// Gets a value indicating whether this overhang has no extension in any direction.
    /// Returns <see langword="true"/> if all overhang values (Left, Top, Right, Bottom) are zero;
    /// otherwise, <see langword="false"/>.
    /// </summary>
    public bool IsEmpty => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;
}
