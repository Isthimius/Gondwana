using Gondwana.Physics.Collisions;
using System.Drawing;

namespace Gondwana.Drawing.Tilesheets.GTS;

/// <summary>
/// Defines a rectangular region within a tilesheet, including layout and collision metadata.
/// </summary>
public sealed class TilesheetRegionDefinition
{
    /// <summary>
    /// Gets or sets the name of the region.
    /// </summary>
    public string Name { get; set; } = TilesheetRegion.DefaultRegionName;

    /// <summary>
    /// Gets or sets the rectangular bounds of this region in the source image.
    /// </summary>
    public Rectangle Area { get; set; }

    /// <summary>
    /// Gets or sets the size of individual frames in this region.
    /// </summary>
    public Size TileSize { get; set; }

    /// <summary>
    /// Gets or sets the spacing between adjacent frames.
    /// </summary>
    public Spacing TilePadding { get; set; } = Spacing.None;

    /// <summary>
    /// Gets or sets the margin around the frame grid.
    /// </summary>
    public Spacing RegionMargin { get; set; } = Spacing.None;

    /// <summary>
    /// Gets or sets the visual overhang beyond the logical tile bounds.
    /// </summary>
    public Spacing Overhang { get; set; } = Spacing.None;

    /// <summary>
    /// Gets or sets the region-level collision adjustment applied to all frames by default.
    /// </summary>
    public CollisionAdjust CollisionAdjust { get; set; } =
        Gondwana.Physics.Collisions.CollisionAdjust.None;

    /// <summary>
    /// Gets or sets the final per-frame collision metadata for this region.
    /// </summary>
    public List<TilesheetFrameDefinition> Frames { get; set; } = [];
}
