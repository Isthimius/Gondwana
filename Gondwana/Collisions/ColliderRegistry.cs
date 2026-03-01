namespace Gondwana.Collisions;

public sealed class ColliderRegistry
{
    private readonly HashSet<ICollider> _static = new();
    private readonly HashSet<ICollider> _dynamic = new();

    public IEnumerable<ICollider> StaticColliders => _static;
    public IEnumerable<ICollider> DynamicColliders => _dynamic;

    public void Register(ICollider collider)
    {
        if (collider is null)
            throw new ArgumentNullException(nameof(collider));

        if (collider.IsStatic)
            _static.Add(collider);
        else
            _dynamic.Add(collider);
    }

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
