using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Scenes.GSCN;

/// <summary>
/// Identifies how a GSCN definition can locate one of its logical GANI dependencies
/// for authoring and tooling purposes.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum SceneAnimationSourceKind
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
/// Describes an authoring-time source for a logical animation key referenced by a GSCN definition.
/// Runtime scene materialization continues to resolve animations through the cycle registry.
/// </summary>
public sealed class SceneAnimationSourceDefinition
{
    /// <summary>
    /// Gets or sets the lookup key of the referenced animation.
    /// </summary>
    public string AnimationKey { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public SceneAnimationSourceKind Kind { get; set; } = SceneAnimationSourceKind.LooseDefinitionFile;
    /// <summary>
    /// Gets or sets the path to the loose GANI animation definition.
    /// </summary>
    public string? GaniPath { get; set; }
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
    /// <param name="animationKey">The lookup key of the animation.</param>
    /// <param name="ganiPath">The path to the loose GANI animation definition.</param>
    /// <returns>A reference to the loose definition file.</returns>
    public static SceneAnimationSourceDefinition Loose(string animationKey, string ganiPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(animationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(ganiPath);
        return new()
        {
            AnimationKey = animationKey,
            Kind = SceneAnimationSourceKind.LooseDefinitionFile,
            GaniPath = ganiPath
        };
    }

    /// <summary>
    /// Creates a reference to a definition stored inside an assets file.
    /// </summary>
    /// <param name="animationKey">The lookup key of the animation.</param>
    /// <param name="assetsFilePath">The path to the assets file.</param>
    /// <param name="assetEntryName">The entry name inside the assets file.</param>
    /// <returns>A reference to the packed definition entry.</returns>
    public static SceneAnimationSourceDefinition Packed(
        string animationKey,
        string assetsFilePath,
        string assetEntryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(animationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetEntryName);
        return new()
        {
            AnimationKey = animationKey,
            Kind = SceneAnimationSourceKind.PackedDefinitionFile,
            AssetsFilePath = assetsFilePath,
            AssetEntryName = assetEntryName
        };
    }
}
