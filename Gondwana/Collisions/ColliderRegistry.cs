namespace Gondwana.Collisions;

/// <summary>
/// Manages registration and querying of colliders, separating them into static and dynamic collections
/// for efficient collision detection.
/// </summary>
public sealed class ColliderRegistry
{
    private readonly HashSet<ICollider> _static = new();
    private readonly HashSet<ICollider> _dynamic = new();

    /// <summary>
    /// Gets the collection of static colliders registered in this registry.
    /// </summary>
    public IEnumerable<ICollider> StaticColliders => _static;
    
    /// <summary>
    /// Gets the collection of dynamic colliders registered in this registry.
    /// </summary>
    public IEnumerable<ICollider> DynamicColliders => _dynamic;

    /// <summary>
    /// Registers a collider with the registry. The collider is added to either the static or dynamic
    /// collection based on its <see cref="ICollider.IsStatic"/> property.
    /// </summary>
    /// <param name="collider">The collider to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="collider"/> is <c>null</c>.</exception>
    public void Register(ICollider collider)
    {
        if (collider is null)
            throw new ArgumentNullException(nameof(collider));

        if (collider.IsStatic)
            _static.Add(collider);
        else
            _dynamic.Add(collider);
    }

    /// <summary>
    /// Unregisters a collider from the registry, removing it from either the static or dynamic collection
    /// based on its <see cref="ICollider.IsStatic"/> property.
    /// </summary>
    /// <param name="collider">The collider to unregister. If <c>null</c>, no action is taken.</param>
    public void Unregister(ICollider collider)
    {
        if (collider is null)
            return;

        if (collider.IsStatic)
            _static.Remove(collider);
        else
            _dynamic.Remove(collider);
    }

    /// <summary>
    /// Broad-phase query: returns colliders overlapping the given AABB that also
    /// match the provided layer mask (bitwise AND with their LayerMask / CollidesWithMask).
    /// </summary>
    /// <param name="area">The axis-aligned bounding box to query within.</param>
    /// <param name="layerMask">The layer mask to test against each collider's <see cref="ICollider.CollidesWith"/> mask.</param>
    /// <param name="collidesWithMask">The collision mask to test against each collider's <see cref="ICollider.CollisionGroup"/>.</param>
    /// <param name="results">The list to populate with matching colliders. This list is cleared before adding results.</param>
    /// <param name="ignore">An optional collider to exclude from the results.</param>
    public void QueryAabb(
        in Aabb area,
        int layerMask,
        int collidesWithMask,
        List<ICollider> results,
        ICollider? ignore = null)
    {
        results.Clear();

        static bool MaskPasses(ICollider c, int layer, int collidesWith)
        {
            if ((c.CollisionGroup & collidesWith) == 0)
                return false;

            if ((layer & c.CollidesWith) == 0)
                return false;

            return true;
        }

        foreach (var c in _static)
        {
            if (ReferenceEquals(c, ignore))
                continue;

            if (!MaskPasses(c, layerMask, collidesWithMask))
                continue;

            if (area.Intersects(c.BoundsWorldPx))
                results.Add(c);
        }

        foreach (var c in _dynamic)
        {
            if (ReferenceEquals(c, ignore))
                continue;

            if (!MaskPasses(c, layerMask, collidesWithMask))
                continue;

            if (area.Intersects(c.BoundsWorldPx))
                results.Add(c);
        }
    }
}
