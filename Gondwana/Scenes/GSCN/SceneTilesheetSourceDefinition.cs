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
    LooseDefinitionFile,
    PackedDefinitionFile
}

/// <summary>
/// Describes an authoring-time source for a logical tilesheet referenced by a GSCN definition.
/// Runtime scene materialization continues to resolve frame references through the tilesheet registry.
/// </summary>
public sealed class SceneTilesheetSourceDefinition
{
    public string Tilesheet { get; set; } = string.Empty;
    public SceneTilesheetSourceKind Kind { get; set; } = SceneTilesheetSourceKind.LooseDefinitionFile;
    public string? GtsPath { get; set; }
    public string? AssetsFilePath { get; set; }
    public string? AssetEntryName { get; set; }

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
