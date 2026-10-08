using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Scenes.GSCN;

/// <summary>
/// Identifies how a GSCN definition can locate one of its logical GTS dependencies
/// for authoring and tooling purposes.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum SceneTilesheetSourceKind
{
    /// <summary>
    /// The definition is stored in a loose file.
    /// </summary>
    LooseDefinitionFile,
    /// <summary>
    /// The definition is stored in an assets file.
    /// </summary>
    PackedDefinitionFile
}

/// <summary>
/// Describes an authoring-time source for a logical tilesheet referenced by a GSCN definition.
/// Runtime scene materialization continues to resolve frame references through the tilesheet registry.
/// </summary>
public sealed class SceneTilesheetSourceDefinition
{
    /// <summary>
    /// Gets or sets the tilesheet.
    /// </summary>
    public string Tilesheet { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public SceneTilesheetSourceKind Kind { get; set; } = SceneTilesheetSourceKind.LooseDefinitionFile;
    /// <summary>
    /// Gets or sets the path to the loose GTS tilesheet definition.
    /// </summary>
    public string? GtsPath { get; set; }
    /// <summary>
    /// Gets or sets the path to the containing assets file.
    /// </summary>
    public string? AssetsFilePath { get; set; }
    /// <summary>
    /// Gets or sets the entry name inside the assets file.
    /// </summary>
    public string? AssetEntryName { get; set; }

    /// <summary>
    /// Creates a reference to a definition stored in a loose file.
    /// </summary>
    /// <param name="tilesheet">The tilesheet.</param>
    /// <param name="gtsPath">The path to the loose GTS tilesheet definition.</param>
    /// <returns>A reference to the loose definition file.</returns>
    public static SceneTilesheetSourceDefinition Loose(string tilesheet, string gtsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tilesheet);
        ArgumentException.ThrowIfNullOrWhiteSpace(gtsPath);
        return new()
        {
            Tilesheet = tilesheet,
            Kind = SceneTilesheetSourceKind.LooseDefinitionFile,
            GtsPath = gtsPath
        };
    }

    /// <summary>
    /// Creates a reference to a definition stored inside an assets file.
    /// </summary>
    /// <param name="tilesheet">The tilesheet.</param>
    /// <param name="assetsFilePath">The path to the assets file.</param>
    /// <param name="assetEntryName">The entry name inside the assets file.</param>
    /// <returns>A reference to the packed definition entry.</returns>
    public static SceneTilesheetSourceDefinition Packed(
        string tilesheet,
        string assetsFilePath,
        string assetEntryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tilesheet);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetEntryName);
        return new()
        {
            Tilesheet = tilesheet,
            Kind = SceneTilesheetSourceKind.PackedDefinitionFile,
            AssetsFilePath = assetsFilePath,
            AssetEntryName = assetEntryName
        };
    }
}
