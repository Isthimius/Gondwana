using Newtonsoft.Json;
using System.Drawing;

namespace Gondwana.Physics.Collisions;

/// <summary>
/// Per-edge inset amounts applied to visual bounds to produce a collision rectangle.
/// </summary>
/// <remarks>
/// <para>
/// Positive values move the corresponding edge inward toward the center of the
/// rectangle. Negative values move the edge outward, expanding the rectangle.
/// </para>
/// </remarks>
public struct CollisionAdjust : IEquatable<CollisionAdjust>
{
    /// <summary>
    /// Gets or sets the number of pixels by which to inset the top edge.
    /// </summary>
    [JsonProperty]
    public int Top { get; set; }

    /// <summary>
    /// Gets or sets the number of pixels by which to inset the bottom edge.
    /// </summary>
    [JsonProperty]
    public int Bottom { get; set; }

    /// <summary>
    /// Gets or sets the number of pixels by which to inset the left edge.
    /// </summary>
    [JsonProperty]
    public int Left { get; set; }

    /// <summary>
    /// Gets or sets the number of pixels by which to inset the right edge.
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
    /// <param name="top">The signed inset amount for the top edge.</param>
    /// <param name="bottom">The signed inset amount for the bottom edge.</param>
    /// <param name="left">The signed inset amount for the left edge.</param>
    /// <param name="right">The signed inset amount for the right edge.</param>
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
        return Rectangle.FromLTRB(
            rectangle.Left + Left,
            rectangle.Top + Top,
            rectangle.Right - Right,
            rectangle.Bottom - Bottom);
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
