namespace Gondwana.Physics.Collisions;

/// <summary>
/// planned for future use
/// </summary>
public static class CollisionDirectionHelper
{
    /// <summary>
    /// Determines the directional relationship of <paramref name="other"/> relative to
    /// <paramref name="primary"/> by comparing their center points.
    /// </summary>
    /// <param name="primary">The primary bounding box used as the directional reference.</param>
    /// <param name="other">The other bounding box whose direction from <paramref name="primary"/> is evaluated.</param>
    /// <returns>
    /// A <see cref="CollisionDirectionFrom"/> value indicating which side of
    /// <paramref name="primary"/> the center of <paramref name="other"/> is on.
    /// </returns>
    public static CollisionDirectionFrom FromCenters(Aabb primary, Aabb other)
    {
        var pc = primary.Center;
        var oc = other.Center;

        float dx = oc.X - pc.X;
        float dy = oc.Y - pc.Y;

        // this assumes Y-down is “south”.
        const float eps = 0.001f;

        bool east = dx > eps;
        bool west = dx < -eps;
        bool south = dy > eps;
        bool north = dy < -eps;

        if (!east && !west && !north && !south)
            return CollisionDirectionFrom.Center;

        if (north && !east && !west) return CollisionDirectionFrom.N;
        if (north && east) return CollisionDirectionFrom.NE;
        if (!north && !south && east) return CollisionDirectionFrom.E;
        if (south && east) return CollisionDirectionFrom.SE;
        if (south && !east && !west) return CollisionDirectionFrom.S;
        if (south && west) return CollisionDirectionFrom.SW;
        if (!north && !south && west) return CollisionDirectionFrom.W;
        if (north && west) return CollisionDirectionFrom.NW;

        return CollisionDirectionFrom.Center;
    }
}
