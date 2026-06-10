namespace Gondwana.Drawing.Tilesheets.GTS;

public sealed class TilesheetDefinition
{
    public string Name { get; set; } = string.Empty;

    public TilesheetImageDefinition Image { get; set; } = new();

    public List<TilesheetRegionDefinition> Regions { get; set; } = [];

    public TilesheetMaskDefinition? Mask { get; set; }

    public bool PremultiplyAlpha { get; set; }
}