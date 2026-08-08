namespace Gondwana.Drawing.Tilesheets.GTS;

/// <summary>
/// Indicates where a tilesheet definition came from.
/// </summary>
public enum TilesheetDefinitionSourceKind
{
    None = 0,
    LooseDefinitionFile = 1,
    PackedDefinitionFile = 2,
    Generated = 3
}
