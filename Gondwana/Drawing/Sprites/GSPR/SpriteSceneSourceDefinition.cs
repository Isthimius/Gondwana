using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Gondwana.Drawing.Sprites.GSPR;

/// <summary>
/// Identifies how a GSPR definition can locate one of its logical GSCN dependencies
/// for authoring and tooling purposes.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum SpriteSceneSourceKind
{
    LooseDefinitionFile,
    PackedDefinitionFile
}

/// <summary>
/// Describes an authoring-time source for a logical scene referenced by a GSPR definition.
/// Runtime sprite materialization resolves scene identities against already loaded scenes.
/// </summary>
public sealed class SpriteSceneSourceDefinition
{
    public string SceneId { get; set; } = string.Empty;
    public SpriteSceneSourceKind Kind { get; set; } = SpriteSceneSourceKind.LooseDefinitionFile;
    public string? GscnPath { get; set; }
    public string? AssetsFilePath { get; set; }
    public string? AssetEntryName { get; set; }

    public static SpriteSceneSourceDefinition Loose(string sceneId, string gscnPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(gscnPath);
        return new()
        {
            SceneId = sceneId,
            Kind = SpriteSceneSourceKind.LooseDefinitionFile,
            GscnPath = gscnPath
        };
    }

    public static SpriteSceneSourceDefinition Packed(
        string sceneId,
        string assetsFilePath,
        string assetEntryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetEntryName);
        return new()
        {
            SceneId = sceneId,
            Kind = SpriteSceneSourceKind.PackedDefinitionFile,
            AssetsFilePath = assetsFilePath,
            AssetEntryName = assetEntryName
        };
    }
}
