using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Physics.Collisions;

/// <summary>
/// Defines the default collision behavior associated with a tilesheet region,
/// frame, or runtime tile.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum TileCollisionType
{
    /// <summary>
    /// Collision is disabled.
    /// </summary>
    None = 0,

    /// <summary>
    /// Collision is enabled and overlapping solid colliders block movement.
    /// </summary>
    Blocking = 1,

    /// <summary>
    /// Collision is enabled and overlaps are reported without blocking movement.
    /// </summary>
    Trigger = 2
}
