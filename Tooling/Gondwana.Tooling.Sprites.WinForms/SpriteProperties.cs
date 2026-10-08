using System.ComponentModel;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Physics.Collisions;

namespace Gondwana.Tooling.Sprites.WinForms;

internal sealed class SpriteProperties(SpriteInstanceDefinition entry)
{
    /// <summary>
    /// Gets the unique identifier.
    /// </summary>
    public Guid Id => entry.Id;
    /// <summary>
    /// Gets the frame.
    /// </summary>
    public string Frame => entry.Frame is { } frame ? $"{frame.Tilesheet} / {frame.RegionName} ({frame.XTile}, {frame.YTile})" : "Unassigned";
    /// <summary>
    /// Gets or sets the optional lookup name.
    /// </summary>
    public string? Nickname { get => entry.Nickname; set => entry.Nickname = value; }
    /// <summary>
    /// Gets or sets the identifier of the referenced scene.
    /// </summary>
    public string SceneId { get => entry.SceneId; set => entry.SceneId = value; }
    /// <summary>
    /// Gets or sets the identifier of the referenced scene layer.
    /// </summary>
    public string SceneLayerId { get => entry.SceneLayerId; set => entry.SceneLayerId = value; }
    /// <summary>
    /// Gets or sets the position.
    /// </summary>
    public PointF Position { get => entry.Position; set => entry.Position = value; }
    /// <summary>
    /// Gets or sets whether this drawable is visible.
    /// </summary>
    public bool Visible { get => entry.Visible; set => entry.Visible = value; }
    /// <summary>
    /// Gets or sets the drawing order within the layer.
    /// </summary>
    public int ZOrder { get => entry.ZOrder; set => entry.ZOrder = value; }
    /// <summary>
    /// Gets or sets the rotation.
    /// </summary>
    public float Rotation { get => entry.Rotation; set => entry.Rotation = value; }
    /// <summary>
    /// Gets or sets the horiz align.
    /// </summary>
    public Gondwana.Drawing.Sprites.HorizontalAlignment HorizAlign { get => entry.HorizAlign; set => entry.HorizAlign = value; }
    /// <summary>
    /// Gets or sets the vert align.
    /// </summary>
    public VerticalAlignment VertAlign { get => entry.VertAlign; set => entry.VertAlign = value; }
    /// <summary>
    /// Gets or sets the nudge x.
    /// </summary>
    public int NudgeX { get => entry.NudgeX; set => entry.NudgeX = value; }
    /// <summary>
    /// Gets or sets the nudge y.
    /// </summary>
    public int NudgeY { get => entry.NudgeY; set => entry.NudgeY = value; }
    /// <summary>
    /// Gets or sets the render size.
    /// </summary>
    public Size RenderSize { get => entry.RenderSize; set => entry.RenderSize = value; }
    /// <summary>
    /// Gets or sets whether scene-layer fog applies to this tile.
    /// </summary>
    public bool EnableFog { get => entry.EnableFog; set => entry.EnableFog = value; }
    /// <summary>
    /// Gets or sets whether adjust collision area by frame is enabled.
    /// </summary>
    public bool AdjustCollisionAreaByFrame { get => entry.AdjustCollisionAreaByFrame; set => entry.AdjustCollisionAreaByFrame = value; }
    /// <summary>
    /// Gets or sets the collision type.
    /// </summary>
    public TileCollisionType CollisionType { get => entry.CollisionType; set => entry.CollisionType = value; }
    /// <summary>
    /// Gets or sets whether collision type by frame is enabled.
    /// </summary>
    public bool CollisionTypeByFrame { get => entry.CollisionTypeByFrame; set => entry.CollisionTypeByFrame = value; }
    /// <summary>
    /// Gets or sets the collision profile name.
    /// </summary>
    public string? CollisionProfileName { get => entry.CollisionProfileName; set => entry.CollisionProfileName = value; }
    /// <summary>
    /// Gets or sets whether this tile participates in collision detection.
    /// </summary>
    public bool CollisionsEnabled { get => entry.CollisionsEnabled; set => entry.CollisionsEnabled = value; }
    /// <summary>
    /// Gets or sets the collision top.
    /// </summary>
    [Category("Collision adjustment")]
    public int CollisionTop { get => entry.AdjustCollisionArea.Top; set { var adjust = entry.AdjustCollisionArea; adjust.Top = value; entry.AdjustCollisionArea = adjust; } }
    /// <summary>
    /// Gets or sets the collision bottom.
    /// </summary>
    [Category("Collision adjustment")]
    public int CollisionBottom { get => entry.AdjustCollisionArea.Bottom; set { var adjust = entry.AdjustCollisionArea; adjust.Bottom = value; entry.AdjustCollisionArea = adjust; } }
    /// <summary>
    /// Gets or sets the collision left.
    /// </summary>
    [Category("Collision adjustment")]
    public int CollisionLeft { get => entry.AdjustCollisionArea.Left; set { var adjust = entry.AdjustCollisionArea; adjust.Left = value; entry.AdjustCollisionArea = adjust; } }
    /// <summary>
    /// Gets or sets the collision right.
    /// </summary>
    [Category("Collision adjustment")]
    public int CollisionRight { get => entry.AdjustCollisionArea.Right; set { var adjust = entry.AdjustCollisionArea; adjust.Right = value; entry.AdjustCollisionArea = adjust; } }
}
