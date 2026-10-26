using System.ComponentModel;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Physics.Collisions;

namespace Gondwana.Tooling.Sprites.WinForms;

internal sealed class SpriteProperties(SpriteInstanceDefinition entry)
{
    /// <summary>Gets the persistent identifier of the edited sprite instance.</summary>
    [Description("Persistent identifier of this sprite instance; used to distinguish it from other placed sprites.")]
    public Guid Id => entry.Id;
    /// <summary>Gets the assigned tilesheet, region, and grid coordinates as display text, or <c>Unassigned</c>.</summary>
    [Description("Current source tilesheet, region, and frame coordinates; assign a frame using the editor's source picker.")]
    public string Frame => entry.Frame is { } frame ? $"{frame.Tilesheet} / {frame.RegionName} ({frame.XTile}, {frame.YTile})" : "Unassigned";

    /// <summary>Logical key of the assigned animation cycle (GANI).</summary>
    [Category("Animation")]
    [Description("Optional GANI cycle key used to animate this sprite; clear the value to use its static frame.")]
    public string? AnimationKey { get => entry.AnimationKey; set => entry.AnimationKey = string.IsNullOrWhiteSpace(value) ? null : value; }

    /// <summary>Starts the selected animation when the sprite is instantiated.</summary>
    [Category("Animation")]
    [Description("Start the assigned animation automatically when the sprite instance is created.")]
    public bool StartAnimation { get => entry.StartAnimation; set => entry.StartAnimation = value; }
    /// <summary>
    /// Gets or sets the optional lookup name.
    /// </summary>
    [Description("Optional friendly name for locating this sprite in game code and tooling.")]
    public string? Nickname { get => entry.Nickname; set => entry.Nickname = value; }
    /// <summary>
    /// Gets or sets the identifier of the referenced scene.
    /// </summary>
    [Description("ID of the scene where this sprite is placed; it must match a referenced scene.")]
    public string SceneId { get => entry.SceneId; set => entry.SceneId = value; }
    /// <summary>
    /// Gets or sets the identifier of the referenced scene layer.
    /// </summary>
    [Description("ID of the scene layer that anchors and draws this sprite.")]
    public string SceneLayerId { get => entry.SceneLayerId; set => entry.SceneLayerId = value; }
    /// <summary>Gets or sets the sprite anchor in scene-layer grid coordinates.</summary>
    [Description("Sprite anchor in the owning scene layer's grid coordinates, not screen pixels.")]
    public PointF Position { get => entry.Position; set => entry.Position = value; }
    /// <summary>
    /// Gets or sets whether this drawable is visible.
    /// </summary>
    [Description("Whether this sprite is rendered. False hides it without removing its saved definition.")]
    public bool Visible { get => entry.Visible; set => entry.Visible = value; }
    /// <summary>
    /// Gets or sets the drawing order within the layer.
    /// </summary>
    [Description("Drawing order among sprites on the same scene layer; higher values appear in front.")]
    public int ZOrder { get => entry.ZOrder; set => entry.ZOrder = value; }
    /// <summary>Gets or sets the sprite rotation in degrees about its rendered center.</summary>
    [Description("Rotation in degrees around the sprite's rendered center.")]
    public float Rotation { get => entry.Rotation; set => entry.Rotation = value; }
    /// <summary>Gets or sets the sprite alignment within the width of its scene-layer tile.</summary>
    [Description("Horizontal alignment of the sprite artwork within its scene-layer tile footprint.")]
    public Gondwana.Drawing.Sprites.HorizontalAlignment HorizAlign { get => entry.HorizAlign; set => entry.HorizAlign = value; }
    /// <summary>Gets or sets the sprite alignment within the height of its scene-layer tile.</summary>
    [Description("Vertical alignment of the sprite artwork within its scene-layer tile footprint.")]
    public VerticalAlignment VertAlign { get => entry.VertAlign; set => entry.VertAlign = value; }
    /// <summary>Gets or sets the horizontal pixel offset applied after positioning and alignment.</summary>
    [Description("Additional horizontal offset in pixels after grid positioning and alignment; positive moves right.")]
    public int NudgeX { get => entry.NudgeX; set => entry.NudgeX = value; }
    /// <summary>Gets or sets the vertical pixel offset applied after positioning and alignment.</summary>
    [Description("Additional vertical offset in pixels after grid positioning and alignment; positive moves down.")]
    public int NudgeY { get => entry.NudgeY; set => entry.NudgeY = value; }
    /// <summary>Gets or sets the explicit rendering dimensions in pixels; an empty size uses the normal sprite-sizing policy.</summary>
    [Description("Explicit width and height for drawing the sprite in pixels; an empty size uses its normal size.")]
    public Size RenderSize { get => entry.RenderSize; set => entry.RenderSize = value; }
    /// <summary>
    /// Gets or sets whether scene-layer fog applies to this tile.
    /// </summary>
    [Description("Whether the owning scene layer's fog treatment affects this sprite.")]
    public bool EnableFog { get => entry.EnableFog; set => entry.EnableFog = value; }
    /// <summary>Gets or sets whether the current frame supplies collision-area adjustments.</summary>
    [Description("Use collision-box adjustments authored on the active tilesheet frame instead of this sprite's instance insets.")]
    public bool AdjustCollisionAreaByFrame { get => entry.AdjustCollisionAreaByFrame; set => entry.AdjustCollisionAreaByFrame = value; }
    /// <summary>Gets or sets the instance collision behavior used when frame collision types are disabled.</summary>
    [Description("Instance collision behavior when CollisionTypeByFrame is false; None disables this collider type.")]
    public TileCollisionType CollisionType { get => entry.CollisionType; set => entry.CollisionType = value; }
    /// <summary>Gets or sets whether the current frame supplies the collision type.</summary>
    [Description("Use the collision behavior from the currently displayed tilesheet frame instead of the instance CollisionType.")]
    public bool CollisionTypeByFrame { get => entry.CollisionTypeByFrame; set => entry.CollisionTypeByFrame = value; }
    /// <summary>Gets or sets the optional name used to resolve the sprite collision profile.</summary>
    [Description("Optional collision profile name that supplies the collision rules for this sprite.")]
    public string? CollisionProfileName { get => entry.CollisionProfileName; set => entry.CollisionProfileName = value; }
    /// <summary>
    /// Gets or sets whether this tile participates in collision detection.
    /// </summary>
    [Description("Whether this sprite participates in collision detection; rendering visibility is independent.")]
    public bool CollisionsEnabled { get => entry.CollisionsEnabled; set => entry.CollisionsEnabled = value; }
    /// <summary>Gets or sets the top collision inset in pixels; negative values expand the collision area.</summary>
    [Category("Collision adjustment")]
    [Description("Pixels removed from the top of the collision area; a negative value extends it upward.")]
    public int CollisionTop { get => entry.AdjustCollisionArea.Top; set { var adjust = entry.AdjustCollisionArea; adjust.Top = value; entry.AdjustCollisionArea = adjust; } }
    /// <summary>Gets or sets the bottom collision inset in pixels; negative values expand the collision area.</summary>
    [Category("Collision adjustment")]
    [Description("Pixels removed from the bottom of the collision area; a negative value extends it downward.")]
    public int CollisionBottom { get => entry.AdjustCollisionArea.Bottom; set { var adjust = entry.AdjustCollisionArea; adjust.Bottom = value; entry.AdjustCollisionArea = adjust; } }
    /// <summary>Gets or sets the left collision inset in pixels; negative values expand the collision area.</summary>
    [Category("Collision adjustment")]
    [Description("Pixels removed from the left of the collision area; a negative value extends it leftward.")]
    public int CollisionLeft { get => entry.AdjustCollisionArea.Left; set { var adjust = entry.AdjustCollisionArea; adjust.Left = value; entry.AdjustCollisionArea = adjust; } }
    /// <summary>Gets or sets the right collision inset in pixels; negative values expand the collision area.</summary>
    [Category("Collision adjustment")]
    [Description("Pixels removed from the right of the collision area; a negative value extends it rightward.")]
    public int CollisionRight { get => entry.AdjustCollisionArea.Right; set { var adjust = entry.AdjustCollisionArea; adjust.Right = value; entry.AdjustCollisionArea = adjust; } }
}
