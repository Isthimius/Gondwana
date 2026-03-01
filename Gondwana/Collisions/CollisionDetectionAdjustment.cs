using Newtonsoft.Json;

namespace Gondwana.Collisions;

/// <summary>
/// Pixel adjustments applied to a Tile's DrawLocation to produce its collision box.
/// Positive values shrink/expand the collision rect relative to the visual rect.
/// </summary>
public struct CollisionDetectionAdjustment
{
    /// <summary>
    /// Gets or sets the pixel adjustment applied to the top edge of the collision box.
    /// </summary>
    [JsonProperty]
    public int Top { get; set; }

    /// <summary>
    /// Gets or sets the pixel adjustment applied to the bottom edge of the collision box.
    /// </summary>
    [JsonProperty]
    public int Bottom { get; set; }

    /// <summary>
    /// Gets or sets the pixel adjustment applied to the left edge of the collision box.
    /// </summary>
    [JsonProperty]
    public int Left { get; set; }

    /// <summary>
    /// Gets or sets the pixel adjustment applied to the right edge of the collision box.
    /// </summary>
    [JsonProperty]
    public int Right { get; set; }

    /// <summary>
    /// Represents a collision detection adjustment with no pixel offsets (all values are zero).
    /// </summary>
    public static readonly CollisionDetectionAdjustment None = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CollisionDetectionAdjustment"/> struct with the specified edge adjustments.
    /// </summary>
    /// <param name="top">The pixel adjustment for the top edge.</param>
    /// <param name="bottom">The pixel adjustment for the bottom edge.</param>
    /// <param name="left">The pixel adjustment for the left edge.</param>
    /// <param name="right">The pixel adjustment for the right edge.</param>
    public CollisionDetectionAdjustment(int top, int bottom, int left, int right)
    {
        Top = top;
        Bottom = bottom;
        Left = left;
        Right = right;
    }
}