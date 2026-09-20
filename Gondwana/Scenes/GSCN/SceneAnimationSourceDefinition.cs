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
    LooseDefinitionFile,
    PackedDefinitionFile
}

/// <summary>
/// Describes an authoring-time source for a logical animation key referenced by a GSCN definition.
/// Runtime scene materialization continues to resolve animations through the cycle registry.
/// </summary>
public sealed class SceneAnimationSourceDefinition
{
    public string AnimationKey { get; set; } = string.Empty;
    public SceneAnimationSourceKind Kind { get; set; } = SceneAnimationSourceKind.LooseDefinitionFile;
    public string? GaniPath { get; set; }
    public string? AssetsFilePath { get; set; }
    public string? AssetEntryName { get; set; }

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
