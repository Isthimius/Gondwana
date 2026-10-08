using System.Drawing;
using Gondwana.Physics.Collisions;

namespace Gondwana.Drawing.Sprites.GSPR;

/// <summary>Stable authored state for one sprite, without runtime object graphs.</summary>
public sealed class SpriteInstanceDefinition
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// Gets or sets the optional lookup name.
    /// </summary>
    public string? Nickname { get; set; }
    /// <summary>
    /// Gets or sets the identifier of the referenced scene.
    /// </summary>
    public string SceneId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the identifier of the referenced scene layer.
    /// </summary>
    public string SceneLayerId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the position.
    /// </summary>
    public PointF Position { get; set; }
    /// <summary>
    /// Gets or sets the frame.
    /// </summary>
    public SpriteFrameDefinition? Frame { get; set; }
    /// <summary>
    /// Gets or sets whether this drawable is visible.
    /// </summary>
    public bool Visible { get; set; } = true;
    /// <summary>
    /// Gets or sets the drawing order within the layer.
    /// </summary>
    public int ZOrder { get; set; } = 1;
    /// <summary>
    /// Gets or sets the rotation.
    /// </summary>
    public float Rotation { get; set; }
    /// <summary>
    /// Gets or sets the horiz align.
    /// </summary>
    public HorizontalAlignment HorizAlign { get; set; } = HorizontalAlignment.Center;
    /// <summary>
    /// Gets or sets the vert align.
    /// </summary>
    public VerticalAlignment VertAlign { get; set; } = VerticalAlignment.Bottom;
    /// <summary>
    /// Gets or sets the nudge x.
    /// </summary>
    public int NudgeX { get; set; }
    /// <summary>
    /// Gets or sets the nudge y.
    /// </summary>
    public int NudgeY { get; set; }
    /// <summary>
    /// Gets or sets the render size.
    /// </summary>
    public Size RenderSize { get; set; }
    /// <summary>
    /// Gets or sets whether scene-layer fog applies to this tile.
    /// </summary>
    public bool EnableFog { get; set; }
    /// <summary>
    /// Gets or sets the adjust collision area.
    /// </summary>
    public CollisionAdjust AdjustCollisionArea { get; set; }
    /// <summary>
    /// Gets or sets whether adjust collision area by frame is enabled.
    /// </summary>
    public bool AdjustCollisionAreaByFrame { get; set; }
    /// <summary>
    /// Gets or sets the collision type.
    /// </summary>
    public TileCollisionType CollisionType { get; set; }
    /// <summary>
    /// Gets or sets whether collision type by frame is enabled.
    /// </summary>
    public bool CollisionTypeByFrame { get; set; }
    /// <summary>
    /// Gets or sets the collision profile name.
    /// </summary>
    public string? CollisionProfileName { get; set; }
    /// <summary>
    /// Gets or sets whether this tile participates in collision detection.
    /// </summary>
    public bool CollisionsEnabled { get; set; }
}
