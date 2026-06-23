using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Physics.Collisions;

/// <summary>
/// Defines how a collider responds to collisions with other colliders.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum CollisionResponseType
{
    /// <summary>
    /// Solid collision response that pushes out overlapping colliders and blocks movement.
    /// </summary>
    Solid,
    
    /// <summary>
    /// Trigger collision response that reports overlaps without applying push-out or blocking movement.
    /// </summary>
    Trigger
}

