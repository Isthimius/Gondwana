namespace Gondwana.Studio.Documents;

/// <summary>
/// Captures provenance details for a tilesheet definition loaded in the editor.
/// </summary>
public sealed class TilesheetDefinitionSource
{
    /// <summary>
    /// Gets or sets the high-level source kind.
    /// </summary>
    public TilesheetDefinitionSourceKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the .gts path when loaded from a loose definition file.
    /// </summary>
    public string? GtsFilePath { get; set; }

    /// <summary>
    /// Gets or sets the .gaf path when loaded from a packed definition file.
    /// </summary>
    public string? AssetsFilePath { get; set; }

    /// <summary>
    /// Gets or sets the packed entry name when loaded from a packed definition file.
    /// </summary>
    public string? AssetEntryName { get; set; }
}

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
