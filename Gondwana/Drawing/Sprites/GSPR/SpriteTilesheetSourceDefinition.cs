using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Drawing.Sprites.GSPR;

/// <summary>
/// Identifies how a GSPR definition can locate one of its logical GTS dependencies
/// for authoring and tooling purposes.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum SpriteTilesheetSourceKind
{
    LooseDefinitionFile,
    PackedDefinitionFile
}

/// <summary>
/// Describes an authoring-time source for a logical tilesheet referenced by a GSPR definition.
/// Runtime scene materialization continues to resolve frame references through the tilesheet registry.
/// </summary>
public sealed class SpriteTilesheetSourceDefinition
{
    public string Tilesheet { get; set; } = string.Empty;
    public SpriteTilesheetSourceKind Kind { get; set; } = SpriteTilesheetSourceKind.LooseDefinitionFile;
    public string? GtsPath { get; set; }
    public string? AssetsFilePath { get; set; }
    public string? AssetEntryName { get; set; }

    public static SpriteTilesheetSourceDefinition Loose(string tilesheet, string gtsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tilesheet);
        ArgumentException.ThrowIfNullOrWhiteSpace(gtsPath);
        return new()
        {
            Tilesheet = tilesheet,
            Kind = SpriteTilesheetSourceKind.LooseDefinitionFile,
            GtsPath = gtsPath
        };
    }

    public static SpriteTilesheetSourceDefinition Packed(
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
            Kind = SpriteTilesheetSourceKind.PackedDefinitionFile,
            AssetsFilePath = assetsFilePath,
            AssetEntryName = assetEntryName
        };
    }
}
