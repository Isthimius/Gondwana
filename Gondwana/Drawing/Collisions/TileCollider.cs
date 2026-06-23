using Gondwana.Physics.Collisions;

namespace Gondwana.Drawing.Collisions;

/// <summary>
/// Provides collision detection capabilities for a <see cref="Tile"/> by implementing the <see cref="ICollider"/> interface.
/// </summary>
public sealed class TileCollider : ICollider
{
    private readonly Tile _tile;

    /// <summary>
    /// Initializes a new instance of the <see cref="TileCollider"/> class for the specified tile.
    /// </summary>
    /// <param name="tile">The tile to provide collision detection for.</param>
    /// <param name="collisionGroup">The bitmask identifying what this collider is (e.g., Player = 1, World = 2).</param>
    /// <param name="collidesWith">The bitmask of collision groups this collider interacts with.</param>
    /// <param name="responseType">The type of collision response. Defaults to <see cref="CollisionResponseType.Solid"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tile"/> is <c>null</c>.</exception>
    public TileCollider(Tile tile, int collisionGroup, int collidesWith, CollisionResponseType responseType = CollisionResponseType.Solid)
    {
        _tile = tile ?? throw new ArgumentNullException(nameof(tile));
        CollisionGroup = collisionGroup;
        CollidesWith = collidesWith;
        ResponseType = responseType;
    }

    /// <summary>
    /// Gets the world-space axis-aligned bounding box in scene world pixels.
    /// </summary>
    public Aabb BoundsWorldPx => Aabb.FromRectangle(_tile.CollisionArea);
    
    /// <summary>
    /// Gets the tile that owns this collider.
    /// </summary>
    public ICollisionEntity Owner => _tile;
    
    /// <summary>
    /// Gets a value indicating whether this is a static, non-moving collider based on the tile's fixed position state.
    /// </summary>
    public bool IsStatic => _tile.IsPositionFixed;
    
    /// <summary>
    /// Gets or sets the bitmask identifying what this collider is (e.g., Player = 1, World = 2, Enemy = 4).
    /// </summary>
    public int CollisionGroup { get; set; }
    
    /// <summary>
    /// Gets or sets the bitmask of collision groups this collider interacts with.
    /// </summary>
    public int CollidesWith { get; set; }
    
    /// <summary>
    /// Gets or sets how this collider responds to collisions (Solid blocks movement, Trigger reports only).
    /// </summary>
    public CollisionResponseType ResponseType { get; set; }
}
