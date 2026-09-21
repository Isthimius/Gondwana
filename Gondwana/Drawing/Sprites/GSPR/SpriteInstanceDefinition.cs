using System.Drawing;
using Gondwana.Physics.Collisions;

namespace Gondwana.Drawing.Sprites.GSPR;

/// <summary>Stable authored state for one sprite, without runtime object graphs.</summary>
public sealed class SpriteInstanceDefinition
{
    public Guid Id { get; set; }
    public string? Nickname { get; set; }
    public string SceneId { get; set; } = string.Empty;
    public string SceneLayerId { get; set; } = string.Empty;
    public PointF Position { get; set; }
    public SpriteFrameDefinition? Frame { get; set; }
    public bool Visible { get; set; } = true;
    public int ZOrder { get; set; } = 1;
    public float Rotation { get; set; }
    public HorizontalAlignment HorizAlign { get; set; } = HorizontalAlignment.Center;
    public VerticalAlignment VertAlign { get; set; } = VerticalAlignment.Bottom;
    public int NudgeX { get; set; }
    public int NudgeY { get; set; }
    public Size RenderSize { get; set; }
    public bool EnableFog { get; set; }
    public CollisionAdjust AdjustCollisionArea { get; set; }
    public bool AdjustCollisionAreaByFrame { get; set; }
    public TileCollisionType CollisionType { get; set; }
    public bool CollisionTypeByFrame { get; set; }
    public string? CollisionProfileName { get; set; }
    public bool CollisionsEnabled { get; set; }
}
