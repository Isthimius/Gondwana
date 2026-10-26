using System.ComponentModel;
using System.Drawing;
using Gondwana.Physics.Collisions;

namespace Gondwana.Drawing.Tilesheets.GTS;

/// <summary>
/// Defines a rectangular region within a tilesheet, including layout and collision metadata.
/// </summary>
public sealed class TilesheetRegionDefinition
{
    /// <summary>
    /// Gets or sets the name of the region.
    /// </summary>
    [Description("Name used to reference this set of frames from a tilesheet, animation, or scene.")]
    public string Name { get; set; } = TilesheetRegion.DefaultRegionName;

    /// <summary>
    /// Gets or sets the rectangular bounds of this region in the source image.
    /// </summary>
    [Description("Rectangle in source-image pixels containing this region's frame grid, including margins and padding.")]
    public Rectangle Area { get; set; }

    /// <summary>
    /// Gets or sets the size of individual frames in this region.
    /// </summary>
    [Description("Width and height in source-image pixels of a single frame in this region.")]
    public Size TileSize { get; set; }

    /// <summary>
    /// Gets or sets the spacing between adjacent frames.
    /// </summary>
    [Description("Unused pixel space surrounding each frame within the grid; the gap between neighboring frames includes both adjacent padding edges.")]
    public Spacing TilePadding { get; set; } = Spacing.None;

    /// <summary>
    /// Gets or sets the margin around the frame grid.
    /// </summary>
    [Description("Unused pixel border between the region rectangle and its frame grid, in source-image pixels.")]
    public Spacing RegionMargin { get; set; } = Spacing.None;

    /// <summary>
    /// Gets or sets the visual overhang beyond the logical tile bounds.
    /// </summary>
    [Description("How far the frame artwork visually extends beyond the logical tile footprint on each side, in pixels.")]
    public Spacing Overhang { get; set; } = Spacing.None;

    /// <summary>
    /// Gets or sets the region-level collision adjustment applied to all frames by default.
    /// </summary>
    [Description("Default collision-box edge insets for frames in this region; positive values shrink and negative values expand the box.")]
    public CollisionAdjust CollisionAdjust { get; set; } = CollisionAdjust.None;

    /// <summary>
    /// Gets or sets the region-level collision type inherited by frames without
    /// an explicit collision-type override.
    /// </summary>
    [Description("Default collision behavior inherited by frames without an explicit per-frame collision type.")]
    public TileCollisionType CollisionType { get; set; } = TileCollisionType.None;

    /// <summary>
    /// Gets or sets per-frame collision metadata for this region. A missing collision
    /// property on a frame means that frame inherits the corresponding region value.
    /// </summary>
    public List<TilesheetFrameDefinition> Frames { get; set; } = [];
}
