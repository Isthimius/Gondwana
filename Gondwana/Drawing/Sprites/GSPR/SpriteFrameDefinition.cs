using Gondwana.Drawing.Tilesheets;

namespace Gondwana.Drawing.Sprites.GSPR;

/// <summary>
/// Stores a lightweight reference to one frame in a registered tilesheet.
/// </summary>
public sealed class SpriteFrameDefinition
{
    /// <summary>
    /// Gets or sets the logical tilesheet name.
    /// </summary>
    public string Tilesheet { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tilesheet region name.
    /// </summary>
    public string RegionName { get; set; } = TilesheetRegion.DefaultRegionName;

    /// <summary>
    /// Gets or sets the zero-based frame column.
    /// </summary>
    public int XTile { get; set; }

    /// <summary>
    /// Gets or sets the zero-based frame row.
    /// </summary>
    public int YTile { get; set; }
}
