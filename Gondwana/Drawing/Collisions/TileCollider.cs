using Gondwana.Collisions;

namespace Gondwana.Drawing.Collisions;

public sealed class TileCollider : ICollider
{
    private readonly Tile _tile;
    private readonly bool _isStatic;

    public TileCollider(Tile tile, int layerMask, int collidesWithMask, CollisionResponse response = CollisionResponse.Solid)
    {
        _tile = tile ?? throw new ArgumentNullException(nameof(tile));
        _isStatic = _tile.IsPositionFixed;
        LayerMask = layerMask;
        CollidesWithMask = collidesWithMask;
        Response = response;
    }

    public Aabb BoundsWorldPx => Aabb.FromRectangle(_tile.CollisionArea);
    public ICollisionEntity Owner => _tile;
    public bool IsStatic => _isStatic;
    public int LayerMask { get; set; }
    public int CollidesWithMask { get; set; }
    public CollisionResponse Response { get; set; }
}
