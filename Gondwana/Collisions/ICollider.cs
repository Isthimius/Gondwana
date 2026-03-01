namespace Gondwana.Collisions;

/// <summary>
/// World-space axis-aligned collider used by the collision system.
/// </summary>
public interface ICollider
{
    /// <summary>World-space AABB in *scene world pixels*.</summary>
    Aabb BoundsWorldPx { get; }

    /// <summary>Back-reference to owning object (Sprite, SceneLayerTile, etc.).</summary>
    ICollisionEntity Owner { get; }

    /// <summary>
    /// True for static, non-moving colliders (walls, tiles, etc.). 
    /// False for dynamic objects (player, NPCs, projectiles).
    /// </summary>
    bool IsStatic { get; }

    /// <summary>Bitmask identifying what this collider is (e.g., Player = 1, World = 2, Enemy = 4, etc.).</summary>
    int CollisionGroup { get; set; }

    /// <summary>Bitmask of what this collider collides *with*.</summary>
    int CollidesWith { get; set; }

    /// <summary>
    /// Gets or sets how this collider responds to collisions (Solid blocks movement, Trigger reports only).
    /// </summary>
    CollisionResponseType ResponseType { get; set; }
}
