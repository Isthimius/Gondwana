using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Drawing.Sprites.GSPR;

/// <summary>Storage form of an optional GANI authoring dependency.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum SpriteAnimationSourceKind
{
    /// <summary>A loose .gani definition file.</summary>
    LooseDefinitionFile,
    /// <summary>A typed GANI entry inside a GAF.</summary>
    PackedDefinitionFile
}

/// <summary>
/// Identifies where authoring tools can find an animation referenced by its logical key.
/// Runtime sprite materialization uses registered Cycle objects rather than these paths.
/// </summary>
public sealed class SpriteAnimationSourceDefinition
{
    /// <summary>The logical animation/Cycle key.</summary>
    public string AnimationKey { get; set; } = string.Empty;
    /// <summary>The form of the source.</summary>
    public SpriteAnimationSourceKind Kind { get; set; } = SpriteAnimationSourceKind.LooseDefinitionFile;
    /// <summary>Loose GANI file path relative to the GSPR, when applicable.</summary>
    public string? GaniPath { get; set; }
    /// <summary>GAF path relative to the GSPR, when applicable.</summary>
    public string? AssetsFilePath { get; set; }
    /// <summary>Typed GANI entry name within the GAF.</summary>
    public string? AssetEntryName { get; set; }

    /// <summary>Creates a loose GANI source reference.</summary>
    public static SpriteAnimationSourceDefinition Loose(string animationKey, string ganiPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(animationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(ganiPath);
        return new() { AnimationKey = animationKey, GaniPath = ganiPath };
    }

    /// <summary>Creates a packed GANI source reference.</summary>
    public static SpriteAnimationSourceDefinition Packed(
        string animationKey, string assetsFilePath, string assetEntryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(animationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetEntryName);
        return new()
        {
            AnimationKey = animationKey,
            Kind = SpriteAnimationSourceKind.PackedDefinitionFile,
            AssetsFilePath = assetsFilePath,
            AssetEntryName = assetEntryName
        };
    }
}
