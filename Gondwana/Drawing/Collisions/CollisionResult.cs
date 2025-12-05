using Gondwana.Drawing.Collisions;

namespace Gondwana.Collision;

public readonly struct CollisionResult
{
    public ICollider Primary { get; }
    public ICollider Other { get; }
    public CollisionDirectionFrom Direction { get; }

    public CollisionResult(ICollider primary, ICollider other, CollisionDirectionFrom direction)
    {
        Primary = primary;
        Other = other;
        Direction = direction;
    }
}
