using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Drawing.Animation.GANI;

/// <summary>
/// Identifies how a GANI definition can locate one of its logical GTS dependencies
/// for authoring and tooling purposes.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AnimationTilesheetSourceKind
{
    /// <summary>
    /// The tilesheet definition is stored as a loose .gts file.
    /// </summary>
    LooseDefinitionFile,

    /// <summary>
    /// The tilesheet definition is stored as an entry inside a Gondwana assets file.
    /// </summary>
    PackedDefinitionFile
}

/// <summary>
/// Describes an authoring-time source for a logical tilesheet referenced by a GANI definition.
/// Runtime materialization continues to resolve frames through the registered tilesheet name.
/// </summary>
public sealed class AnimationTilesheetSourceDefinition
{
    /// <summary>
    /// Gets or sets the logical tilesheet name used by animation frame references.
    /// </summary>
    public string Tilesheet { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how the referenced GTS definition is stored.
    /// </summary>
    public AnimationTilesheetSourceKind Kind { get; set; } =
        AnimationTilesheetSourceKind.LooseDefinitionFile;

    /// <summary>
    /// Gets or sets the loose .gts path. Relative paths are resolved from the containing .gani file.
    /// </summary>
    public string? GtsPath { get; set; }

    /// <summary>
    /// Gets or sets the assets-file path for a packed GTS definition.
    /// Relative paths are resolved from the containing .gani file.
    /// </summary>
    public string? AssetsFilePath { get; set; }

    /// <summary>
    /// Gets or sets the GTS entry name inside <see cref="AssetsFilePath"/>.
    /// </summary>
    public string? AssetEntryName { get; set; }

    /// <summary>
    /// Creates a loose-GTS source reference.
    /// </summary>
    public static AnimationTilesheetSourceDefinition Loose(
        string tilesheet,
        string gtsPath)
    {
        if (string.IsNullOrWhiteSpace(tilesheet))
            throw new ArgumentException("Tilesheet name must be a non-empty string.", nameof(tilesheet));

        if (string.IsNullOrWhiteSpace(gtsPath))
            throw new ArgumentException("GTS path must be a non-empty string.", nameof(gtsPath));

        return new AnimationTilesheetSourceDefinition
        {
            Tilesheet = tilesheet,
            Kind = AnimationTilesheetSourceKind.LooseDefinitionFile,
            GtsPath = gtsPath
        };
    }

    /// <summary>
    /// Creates a packed-GTS source reference.
    /// </summary>
    public static AnimationTilesheetSourceDefinition Packed(
        string tilesheet,
        string assetsFilePath,
        string assetEntryName)
    {
        if (string.IsNullOrWhiteSpace(tilesheet))
            throw new ArgumentException("Tilesheet name must be a non-empty string.", nameof(tilesheet));

        if (string.IsNullOrWhiteSpace(assetsFilePath))
            throw new ArgumentException("Assets file path must be a non-empty string.", nameof(assetsFilePath));

        if (string.IsNullOrWhiteSpace(assetEntryName))
            throw new ArgumentException("Asset entry name must be a non-empty string.", nameof(assetEntryName));

        return new AnimationTilesheetSourceDefinition
        {
            Tilesheet = tilesheet,
            Kind = AnimationTilesheetSourceKind.PackedDefinitionFile,
            AssetsFilePath = assetsFilePath,
            AssetEntryName = assetEntryName
        };
    }
}
