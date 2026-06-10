using System.Drawing;

namespace Gondwana.Drawing.Tilesheets.GTS;

public sealed class TilesheetRegionDefinition
{
    public string Name { get; set; } = TilesheetRegion.DefaultRegionName;

    public Rectangle Area { get; set; }

    public Size TileSize { get; set; }

    public Spacing TilePadding { get; set; } = Spacing.None;

    public Spacing RegionMargin { get; set; } = Spacing.None;

    public Spacing Overhang { get; set; } = Spacing.None;
}