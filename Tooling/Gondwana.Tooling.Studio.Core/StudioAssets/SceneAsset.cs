using Newtonsoft.Json;

namespace Gondwana.Tooling.StudioAssets;

/// <summary>
/// SceneAsset.
/// </summary>
public sealed class SceneAsset
{
    /// <summary>
    /// Gets or sets the scene layers.
    /// </summary>
    [JsonProperty("layers")]
    public List<SceneLayerAsset> Layers { get; set; } = [];

    /// <summary>
    /// Gets or sets the scene entities.
    /// </summary>
    [JsonProperty("entities")]
    public List<SceneEntityAsset> Entities { get; set; } = [];

    /// <summary>
    /// Gets or sets the scene colliders.
    /// </summary>
    [JsonProperty("colliders")]
    public List<SceneColliderAsset> Colliders { get; set; } = [];
}

/// <summary>
/// SceneLayerAsset.
/// </summary>
public sealed class SceneLayerAsset
{
    /// <summary>
    /// Gets or sets the layer name.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = "layer";

    /// <summary>
    /// Gets or sets the layer parallax.
    /// </summary>
    [JsonProperty("parallax")]
    public float Parallax { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the tilesheet path.
    /// </summary>
    [JsonProperty("tilesheet")]
    public string Tilesheet { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the layer tiles.
    /// </summary>
    [JsonProperty("tiles")]
    public List<SceneLayerTileAsset> Tiles { get; set; } = [];
}

/// <summary>
/// SceneLayerTileAsset.
/// </summary>
public sealed class SceneLayerTileAsset
{
    /// <summary>
    /// Gets or sets the tile index.
    /// </summary>
    [JsonProperty("tileIndex")]
    public int TileIndex { get; set; }

    /// <summary>
    /// Gets or sets the grid X coordinate.
    /// </summary>
    [JsonProperty("x")]
    public int X { get; set; }

    /// <summary>
    /// Gets or sets the grid Y coordinate.
    /// </summary>
    [JsonProperty("y")]
    public int Y { get; set; }
}

/// <summary>
/// SceneEntityAsset.
/// </summary>
public sealed class SceneEntityAsset
{
    /// <summary>
    /// Gets or sets the entity name.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the world X coordinate.
    /// </summary>
    [JsonProperty("x")]
    public float X { get; set; }

    /// <summary>
    /// Gets or sets the world Y coordinate.
    /// </summary>
    [JsonProperty("y")]
    public float Y { get; set; }
}

/// <summary>
/// SceneColliderAsset.
/// </summary>
public sealed class SceneColliderAsset
{
    /// <summary>
    /// Gets or sets the collider X coordinate.
    /// </summary>
    [JsonProperty("x")]
    public float X { get; set; }

    /// <summary>
    /// Gets or sets the collider Y coordinate.
    /// </summary>
    [JsonProperty("y")]
    public float Y { get; set; }

    /// <summary>
    /// Gets or sets the collider width.
    /// </summary>
    [JsonProperty("width")]
    public float Width { get; set; }

    /// <summary>
    /// Gets or sets the collider height.
    /// </summary>
    [JsonProperty("height")]
    public float Height { get; set; }
}
