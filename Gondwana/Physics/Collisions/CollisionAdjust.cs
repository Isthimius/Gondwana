using Newtonsoft.Json;
using System.Drawing;

namespace Gondwana.Physics.Collisions;

/// <summary>
/// Pixel adjustments applied to visual bounds to produce a collision rectangle.
/// </summary>
/// <remarks>
/// <para>
/// The top and left values move those edges relative to the visual rectangle.
/// The bottom and right values adjust the rectangle's far edges. The same
/// collision calculation is preserved.
/// </para>
/// </remarks>
public struct CollisionAdjust : IEquatable<CollisionAdjust>
{
    /// <summary>
    /// Gets or sets the pixel adjustment applied to the top edge.
    /// </summary>
    [JsonProperty]
    public int Top { get; set; }

    /// <summary>
    /// Gets or sets the pixel adjustment applied to the bottom edge.
    /// </summary>
    [JsonProperty]
    public int Bottom { get; set; }

    /// <summary>
    /// Gets or sets the pixel adjustment applied to the left edge.
    /// </summary>
    [JsonProperty]
    public int Left { get; set; }

    /// <summary>
    /// Gets or sets the pixel adjustment applied to the right edge.
    /// </summary>
    [JsonProperty]
    public int Right { get; set; }

    /// <summary>
    /// Represents an adjustment with no pixel offsets.
    /// </summary>
    public static readonly CollisionAdjust None = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CollisionAdjust"/> struct.
    /// </summary>
    /// <param name="top">The pixel adjustment for the top edge.</param>
    /// <param name="bottom">The pixel adjustment for the bottom edge.</param>
    /// <param name="left">The pixel adjustment for the left edge.</param>
    /// <param name="right">The pixel adjustment for the right edge.</param>
    public CollisionAdjust(int top, int bottom, int left, int right)
    {
        Top = top;
        Bottom = bottom;
        Left = left;
        Right = right;
    }

    /// <summary>
    /// Applies this adjustment to the supplied rectangle.
    /// </summary>
    /// <param name="rectangle">The unadjusted visual rectangle.</param>
    /// <returns>The derived collision rectangle.</returns>
    public readonly Rectangle ApplyTo(Rectangle rectangle)
    {
        rectangle.Y += Top;
        rectangle.X += Left;
        rectangle.Height += Bottom - Top;
        rectangle.Width += Right - Left;
        return rectangle;
    }

    /// <inheritdoc/>
    public readonly bool Equals(CollisionAdjust other) =>
        Top == other.Top &&
        Bottom == other.Bottom &&
        Left == other.Left &&
        Right == other.Right;

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) =>
        obj is CollisionAdjust other && Equals(other);

    /// <inheritdoc/>
    public override readonly int GetHashCode() =>
        HashCode.Combine(Top, Bottom, Left, Right);

    /// <summary>
    /// Determines whether two adjustments are equal.
    /// </summary>
    public static bool operator ==(CollisionAdjust left, CollisionAdjust right) =>
        left.Equals(right);

    /// <summary>
    /// Determines whether two adjustments are not equal.
    /// </summary>
    public static bool operator !=(CollisionAdjust left, CollisionAdjust right) =>
        !left.Equals(right);
}
