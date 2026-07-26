using Gondwana.Physics.Collisions;

namespace Gondwana.Drawing.Tilesheets.GTS;

/// <summary>
/// Defines collision metadata for one frame in a tilesheet region.
/// </summary>
public sealed class TilesheetFrameDefinition
{
    /// <summary>
    /// Gets or sets the zero-based frame column within the region.
    /// </summary>
    public int XTile { get; set; }

    /// <summary>
    /// Gets or sets the zero-based frame row within the region.
    /// </summary>
    public int YTile { get; set; }

    /// <summary>
    /// Gets or sets the frame-specific collision adjustment.
    /// A missing value inherits the region adjustment for backward compatibility.
    /// </summary>
    public CollisionAdjust? CollisionAdjust { get; set; }
}
