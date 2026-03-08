namespace Gondwana.Collisions;

/// <summary>
/// planned for future use
/// </summary>
public static class CollisionDirectionHelper
{
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
