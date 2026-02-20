using Newtonsoft.Json;

namespace Gondwana.Drawing.Collisions;

/// <summary>
/// Pixel adjustments applied to a Tile's DrawLocation to produce its collision box.
/// Positive values shrink/expand the collision rect relative to the visual rect.
/// </summary>
public struct CollisionDetectionAdjustment
{
    [JsonProperty]
    public int Top { get; set; }

    [JsonProperty]
    public int Bottom { get; set; }

    [JsonProperty]
    public int Left { get; set; }

    [JsonProperty]
    public int Right { get; set; }

    public static readonly CollisionDetectionAdjustment None = new();

    public CollisionDetectionAdjustment(int top, int bottom, int left, int right)
    {
        Top = top;
        Bottom = bottom;
        Left = left;
        Right = right;
    }
}