using System.Drawing;

namespace Gondwana.Drawing.Tilesheets.GTS;

/// <summary>
/// Defines a rectangular region within a tilesheet, including its location, tile dimensions, and spacing parameters.
/// </summary>
/// <remarks>
/// A tilesheet can contain multiple regions, each representing a distinct grid of tiles with its own layout parameters.
/// This class encapsulates all the information needed to correctly parse and extract individual tiles from a region.
/// </remarks>
public sealed class TilesheetRegionDefinition
{
    /// <summary>
    /// Gets or sets the name of the region.
    /// </summary>
    /// <value>Defaults to <see cref="TilesheetRegion.DefaultRegionName"/>.</value>
    /// <remarks>
    /// Region names are used to reference specific regions within a tilesheet, allowing multiple
    /// tile layouts to coexist within a single image file.
    /// </remarks>
    public string Name { get; set; } = TilesheetRegion.DefaultRegionName;

    /// <summary>
    /// Gets or sets the rectangular area defining the bounds of this region within the tilesheet image.
    /// </summary>
    /// <remarks>
    /// The area is specified in pixel coordinates relative to the top-left corner of the tilesheet image.
    /// </remarks>
    public Rectangle Area { get; set; }

    /// <summary>
    /// Gets or sets the size of individual tiles within this region.
    /// </summary>
    /// <remarks>
    /// All tiles within a region share the same dimensions. The tile size does not include padding or margins.
    /// </remarks>
    public Size TileSize { get; set; }

    /// <summary>
    /// Gets or sets the spacing between adjacent tiles within the region.
    /// </summary>
    /// <value>Defaults to <see cref="Spacing.None"/>.</value>
    /// <remarks>
    /// Tile padding defines the gap between tiles in the grid. This is useful for tilesheets where tiles
    /// are separated by borders or gutters to prevent texture bleeding.
    /// </remarks>
    public Spacing TilePadding { get; set; } = Spacing.None;

    /// <summary>
    /// Gets or sets the margin space around the entire region.
    /// </summary>
    /// <value>Defaults to <see cref="Spacing.None"/>.</value>
    /// <remarks>
    /// Region margin defines the space between the region's boundary (<see cref="Area"/>) and the first tile.
    /// This accounts for any border or padding around the tile grid itself.
    /// </remarks>
    public Spacing RegionMargin { get; set; } = Spacing.None;

    /// <summary>
    /// Gets or sets the overhang spacing that extends beyond the standard tile boundaries.
    /// </summary>
    /// <value>Defaults to <see cref="Spacing.None"/>.</value>
    /// <remarks>
    /// Overhang allows tiles to visually extend beyond their logical grid boundaries, which is useful
    /// for tiles with effects like shadows, glows, or other visual elements that bleed into adjacent
    /// tile spaces while maintaining proper grid alignment.
    /// </remarks>
    public Spacing Overhang { get; set; } = Spacing.None;
}