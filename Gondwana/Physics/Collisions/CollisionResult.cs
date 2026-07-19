namespace Gondwana.Physics.Collisions;

/// <summary>
/// planned for future use
/// </summary>
public readonly struct CollisionResult
{
    /// <summary>
    /// Gets the collider that initiated the collision check.
    /// </summary>
    public ICollider Primary { get; }

    /// <summary>
    /// Gets the collider that was found colliding with <see cref="Primary"/>.
    /// </summary>
    public ICollider Other { get; }

    /// <summary>
    /// Gets the detected direction of <see cref="Other"/> relative to <see cref="Primary"/>.
    /// </summary>
    public CollisionDirectionFrom Direction { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CollisionResult"/> struct.
    /// </summary>
    /// <param name="primary">The collider used as the primary collision reference.</param>
    /// <param name="other">The collider that collided with <paramref name="primary"/>.</param>
    /// <param name="direction">The detected direction of the collision.</param>
    public CollisionResult(ICollider primary, ICollider other, CollisionDirectionFrom direction)
    {
        Primary = primary;
        Other = other;
        Direction = direction;
    }
}
