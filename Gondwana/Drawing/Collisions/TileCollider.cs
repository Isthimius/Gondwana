using Gondwana.Collisions;

namespace Gondwana.Drawing.Collisions;

public sealed class TileCollider : ICollider
{
    private readonly Tile _tile;

    public TileCollider(Tile tile, int collisionGroup = 0, int collidesWith = 0, CollisionResponseType responseType = CollisionResponseType.Solid)
    {
        _tile = tile ?? throw new ArgumentNullException(nameof(tile));
        CollisionGroup = collisionGroup;
        CollidesWith = collidesWith;
        ResponseType = responseType;
    }

    public Aabb BoundsWorldPx => Aabb.FromRectangle(_tile.CollisionArea);
    public ICollisionEntity Owner => _tile;
    public bool IsStatic => _tile.IsPositionFixed;
    public int CollisionGroup { get; set; }
    public int CollidesWith { get; set; }
    public CollisionResponseType ResponseType { get; set; }
}
