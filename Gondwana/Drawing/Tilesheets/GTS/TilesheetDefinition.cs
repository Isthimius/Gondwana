namespace Gondwana.Drawing.Tilesheets.GTS;

/// <summary>
/// Represents the root definition of a tilesheet in the GTS (Gondwana Tilesheet) file format.
/// </summary>
public sealed class TilesheetDefinition
{
    /// <summary>
    /// Gets or sets the name of the tilesheet.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the image definition that specifies the source image for the tilesheet.
    /// </summary>
    public TilesheetImageDefinition Image { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of region definitions that define subdivisions within the tilesheet.
    /// </summary>
    public List<TilesheetRegionDefinition> Regions { get; set; } = [];

    /// <summary>
    /// Gets or sets the mask definition for color transparency, or <see langword="null"/> if no mask is applied.
    /// </summary>
    public TilesheetMaskDefinition? Mask { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether alpha should be premultiplied when loading the tilesheet image.
    /// </summary>
    public bool PremultiplyAlpha { get; set; }
}