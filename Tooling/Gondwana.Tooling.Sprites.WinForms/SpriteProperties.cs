using System.ComponentModel;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Physics.Collisions;

namespace Gondwana.Tooling.Sprites.WinForms;

internal sealed class SpriteProperties(SpriteInstanceDefinition entry)
{
    public Guid Id => entry.Id;
    public string Frame => entry.Frame is { } frame ? $"{frame.Tilesheet} / {frame.RegionName} ({frame.XTile}, {frame.YTile})" : "Unassigned";
    public string? Nickname { get => entry.Nickname; set => entry.Nickname = value; }
    public string SceneId { get => entry.SceneId; set => entry.SceneId = value; }
    public string SceneLayerId { get => entry.SceneLayerId; set => entry.SceneLayerId = value; }
    public PointF Position { get => entry.Position; set => entry.Position = value; }
    public bool Visible { get => entry.Visible; set => entry.Visible = value; }
    public int ZOrder { get => entry.ZOrder; set => entry.ZOrder = value; }
    public float Rotation { get => entry.Rotation; set => entry.Rotation = value; }
    public Gondwana.Drawing.Sprites.HorizontalAlignment HorizAlign { get => entry.HorizAlign; set => entry.HorizAlign = value; }
    public VerticalAlignment VertAlign { get => entry.VertAlign; set => entry.VertAlign = value; }
    public int NudgeX { get => entry.NudgeX; set => entry.NudgeX = value; }
    public int NudgeY { get => entry.NudgeY; set => entry.NudgeY = value; }
    public Size RenderSize { get => entry.RenderSize; set => entry.RenderSize = value; }
    public bool EnableFog { get => entry.EnableFog; set => entry.EnableFog = value; }
    public bool AdjustCollisionAreaByFrame { get => entry.AdjustCollisionAreaByFrame; set => entry.AdjustCollisionAreaByFrame = value; }
    public TileCollisionType CollisionType { get => entry.CollisionType; set => entry.CollisionType = value; }
    public bool CollisionTypeByFrame { get => entry.CollisionTypeByFrame; set => entry.CollisionTypeByFrame = value; }
    public string? CollisionProfileName { get => entry.CollisionProfileName; set => entry.CollisionProfileName = value; }
    public bool CollisionsEnabled { get => entry.CollisionsEnabled; set => entry.CollisionsEnabled = value; }
    [Category("Collision adjustment")]
    public int CollisionTop { get => entry.AdjustCollisionArea.Top; set { var adjust = entry.AdjustCollisionArea; adjust.Top = value; entry.AdjustCollisionArea = adjust; } }
    [Category("Collision adjustment")]
    public int CollisionBottom { get => entry.AdjustCollisionArea.Bottom; set { var adjust = entry.AdjustCollisionArea; adjust.Bottom = value; entry.AdjustCollisionArea = adjust; } }
    [Category("Collision adjustment")]
    public int CollisionLeft { get => entry.AdjustCollisionArea.Left; set { var adjust = entry.AdjustCollisionArea; adjust.Left = value; entry.AdjustCollisionArea = adjust; } }
    [Category("Collision adjustment")]
    public int CollisionRight { get => entry.AdjustCollisionArea.Right; set { var adjust = entry.AdjustCollisionArea; adjust.Right = value; entry.AdjustCollisionArea = adjust; } }
}
