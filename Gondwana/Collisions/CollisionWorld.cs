using System.Collections.Generic;

namespace Gondwana.Collisions;

public sealed class CollisionWorld
{
    private readonly List<ICollider> _static = new();
    private readonly List<ICollider> _dynamic = new();

    public IReadOnlyList<ICollider> StaticColliders => _static;
    public IReadOnlyList<ICollider> DynamicColliders => _dynamic;

    public void Register(ICollider collider)
    {
        if (collider.IsStatic)
        {
            if (!_static.Contains(collider))
                _static.Add(collider);
        }
        else
        {
            if (!_dynamic.Contains(collider))
                _dynamic.Add(collider);
        }
    }

    public void Unregister(ICollider collider)
    {
        _static.Remove(collider);
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
        List<ICollider> results)
    {
        results.Clear();

        // helper
        static bool MaskPasses(ICollider c, int layer, int collidesWith)
        {
            if ((c.LayerMask & collidesWith) == 0) return false;
            if ((layer & c.CollidesWithMask) == 0) return false;
            return true;
        }

        foreach (var c in _static)
        {
            if (!MaskPasses(c, layerMask, collidesWithMask)) continue;
            if (area.Intersects(c.BoundsWorldPx))
                results.Add(c);
        }

        foreach (var c in _dynamic)
        {
            if (!MaskPasses(c, layerMask, collidesWithMask)) continue;
            if (area.Intersects(c.BoundsWorldPx))
                results.Add(c);
        }
    }
}
