using Gondwana.Collisions;

namespace Gondwana.Drawing.Collisions;

public sealed class TileCollider : ICollider
{
    private readonly Tile _tile;

    public TileCollider(Tile tile, int layerMask = 0, int collidesWithMask = 0, CollisionResponseType response = CollisionResponseType.Solid)
    {
        _tile = tile ?? throw new ArgumentNullException(nameof(tile));
        CollisionGroup = layerMask;
        CollidesWith = collidesWithMask;
        ResponseType = response;
    }

    public Aabb BoundsWorldPx => Aabb.FromRectangle(_tile.CollisionArea);
    public ICollisionEntity Owner => _tile;
    public bool IsStatic => _tile.IsPositionFixed;
    public int CollisionGroup { get; set; }
    public int CollidesWith { get; set; }
    public CollisionResponseType ResponseType { get; set; }
}
