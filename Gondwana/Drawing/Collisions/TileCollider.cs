using Gondwana.Drawing;

namespace Gondwana.Collision;

public sealed class TileCollider : ICollider
{
    private readonly Tile _tile;
    private readonly int _layerMask;
    private readonly int _collidesWithMask;
    private readonly bool _isStatic;

    public TileCollider(Tile tile, int layerMask, int collidesWithMask, bool isStatic = false)
    {
        _tile = tile ?? throw new ArgumentNullException(nameof(tile));
        _layerMask = layerMask;
        _collidesWithMask = collidesWithMask;
        _isStatic = isStatic;
    }

    public Aabb BoundsWorldPx => Aabb.FromRectangle(_tile.CollisionArea);
    public bool IsStatic => _isStatic;
    public int LayerMask => _layerMask;
    public int CollidesWithMask => _collidesWithMask;
    public Tile Owner => _tile;
}
