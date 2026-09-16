namespace Gondwana.Physics.Collisions;

/// <summary>A canonical collider and the world-space bounds of one queried periodic instance.</summary>
/// <param name="Collider">The original registered collider; never a clone.</param>
/// <param name="BoundsWorldPx">Translated bounds to use for overlap and resolution.</param>
public readonly record struct ColliderInstance(ICollider Collider, Aabb BoundsWorldPx);
