using Newtonsoft.Json;

namespace Gondwana.Physics.Collisions;

/// <summary>
/// Describes a reusable, scene-level collision-filtering role using collision
/// group names rather than scene-specific integer masks.
/// </summary>
public sealed class CollisionProfile
{
    /// <summary>
    /// Initializes a collision profile.
    /// </summary>
    [JsonConstructor]
    public CollisionProfile(
        string name,
        string collisionGroup,
        IEnumerable<string>? collidesWith = null,
        bool collidesWithAll = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collision profile name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(collisionGroup))
            throw new ArgumentException("Collision profile group cannot be empty.", nameof(collisionGroup));

        Name = name;
        CollisionGroup = collisionGroup;
        CollidesWith = collidesWith?.ToList() ?? [];
        CollidesWithAll = collidesWithAll;
    }

    /// <summary>
    /// Gets the stable profile name.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets or sets the registered collision group name assigned to this profile.
    /// </summary>
    public string CollisionGroup { get; set; }

    /// <summary>
    /// Gets the registered group names with which this profile interacts.
    /// </summary>
    public List<string> CollidesWith { get; private set; }

    /// <summary>
    /// Gets or sets whether this profile interacts with every collision group.
    /// </summary>
    public bool CollidesWithAll { get; set; }

    /// <summary>
    /// Resolves this profile's own group through the supplied scene registry.
    /// </summary>
    public int ResolveCollisionGroup(CollisionGroupRegistry groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return groups.Get(CollisionGroup);
    }

    /// <summary>
    /// Resolves this profile's interaction mask through the supplied scene registry.
    /// </summary>
    public int ResolveCollidesWith(CollisionGroupRegistry groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return CollidesWithAll
            ? CollisionMasks.All
            : groups.GetMask(CollidesWith);
    }
}
