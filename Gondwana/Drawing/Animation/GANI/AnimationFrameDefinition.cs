using Gondwana.Drawing.Tilesheets;

namespace Gondwana.Drawing.Animation.GANI;

/// <summary>
/// Identifies one tilesheet frame used by a GANI animation.
/// </summary>
public sealed class AnimationFrameDefinition
{
    /// <summary>Display duration in seconds; null uses the animation's ThrottleTime.</summary>
    public double? DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the logical name of the registered tilesheet.
    /// </summary>
    public string Tilesheet { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tilesheet region name.
    /// </summary>
    public string RegionName { get; set; } = TilesheetRegion.DefaultRegionName;

    /// <summary>
    /// Gets or sets the zero-based frame column within the region.
    /// </summary>
    public int XTile { get; set; }

    /// <summary>
    /// Gets or sets the zero-based frame row within the region.
    /// </summary>
    public int YTile { get; set; }
}
