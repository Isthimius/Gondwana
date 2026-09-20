using Gondwana.Physics.Collisions;

namespace Gondwana.Scenes.GSCN;

/// <summary>
/// Defines persistent state for one tile cell in a GSCN scene layer.
/// </summary>
public sealed class SceneLayerTileDefinition
{
    /// <summary>
    /// Gets or sets the zero-based tile column.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Gets or sets the zero-based tile row.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Gets or sets the persistent tile identifier. Guid.Empty leaves the generated runtime ID unchanged.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets an optional logical nickname.
    /// </summary>
    public string? Nickname { get; set; }

    /// <summary>
    /// Gets or sets whether the tile is visible.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Gets or sets the lightweight tilesheet frame reference, or null for an unassigned frame.
    /// </summary>
    public SceneFrameDefinition? Frame { get; set; }

    /// <summary>
    /// Gets or sets whether an animator should exist for this tile even when no animation is assigned.
    /// An <see cref="AnimationKey"/> also causes an animator to be materialized.
    /// </summary>
    public bool EnableAnimator { get; set; }

    /// <summary>
    /// Gets or sets the logical GANI/cycle key assigned to this tile, or null when no animation is assigned.
    /// </summary>
    public string? AnimationKey { get; set; }

    /// <summary>
    /// Gets or sets whether the assigned animation should begin playing when the scene is materialized.
    /// </summary>
    public bool StartAnimation { get; set; }

    /// <summary>
    /// Gets or sets whether fog affects the tile.
    /// </summary>
    public bool EnableFog { get; set; }

    /// <summary>
    /// Gets or sets whether frame changes update collision adjustment.
    /// </summary>
    public bool AdjustCollisionAreaByFrame { get; set; }

    /// <summary>
    /// Gets or sets the tile collision adjustment.
    /// </summary>
    public CollisionAdjust AdjustCollisionArea { get; set; } = CollisionAdjust.None;

    /// <summary>
    /// Gets or sets the tile collision behavior.
    /// </summary>
    public TileCollisionType CollisionType { get; set; } = TileCollisionType.None;

    /// <summary>
    /// Gets or sets whether frame changes update collision type.
    /// </summary>
    public bool CollisionTypeByFrame { get; set; }

    /// <summary>
    /// Gets or sets the named scene collision profile assigned to the tile.
    /// </summary>
    public string? CollisionProfileName { get; set; }

}
