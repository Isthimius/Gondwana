using Gondwana.Collisions;

namespace Gondwana.Drawing.Collisions;

public sealed class TileCollider : ICollider
{
    private readonly Tile _tile;

    public TileCollider(Tile tile, int layerMask = 0, int collidesWithMask = 0, CollisionResponseType response = CollisionResponseType.Solid)
    {
        _tile = tile ?? throw new ArgumentNullException(nameof(tile));
        LayerMask = layerMask;
        CollidesWithMask = collidesWithMask;
        Response = response;
    }

    public Aabb BoundsWorldPx => Aabb.FromRectangle(_tile.CollisionArea);
    public ICollisionEntity Owner => _tile;
    public bool IsStatic => _tile.IsPositionFixed;
    public int LayerMask { get; set; }
    public int CollidesWithMask { get; set; }
    public CollisionResponseType Response { get; set; }
}
