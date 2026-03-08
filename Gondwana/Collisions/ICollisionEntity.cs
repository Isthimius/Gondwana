using System.Drawing;

namespace Gondwana.Collisions;

/// <summary>
/// Represents an entity that participates in the collision system and has a defined collision area.
/// </summary>
public interface ICollisionEntity
{
    /// <summary>
    /// Gets the collision area of this entity in world pixel coordinates.
    /// </summary>
    Rectangle CollisionArea { get; }
}
