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
/// Describes an authoring-time source for a logical scene referenced by a GSPR definition.
/// Runtime sprite materialization resolves scene identities against already loaded scenes.
/// </summary>
public sealed class SpriteSceneSourceDefinition
{
    /// <summary>
    /// Gets or sets the identifier of the referenced scene.
    /// </summary>
    public string SceneId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public SpriteSceneSourceKind Kind { get; set; } = SpriteSceneSourceKind.LooseDefinitionFile;
    /// <summary>
    /// Gets or sets the path to the loose GSCN scene definition.
    /// </summary>
    public string? GscnPath { get; set; }
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
    /// <param name="sceneId">The scene id.</param>
    /// <param name="gscnPath">The path to the loose GSCN scene definition.</param>
    /// <returns>A reference to the loose definition file.</returns>
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

    /// <summary>
    /// Creates a reference to a definition stored inside an assets file.
    /// </summary>
    /// <param name="sceneId">The scene id.</param>
    /// <param name="assetsFilePath">The path to the assets file.</param>
    /// <param name="assetEntryName">The entry name inside the assets file.</param>
    /// <returns>A reference to the packed definition entry.</returns>
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
