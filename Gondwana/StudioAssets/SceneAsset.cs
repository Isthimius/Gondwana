using Newtonsoft.Json;

namespace Gondwana.StudioAssets;

public sealed class SceneAsset
{
    [JsonProperty("layers")]
    public List<SceneLayerAsset> Layers { get; set; } = [];

    [JsonProperty("entities")]
    public List<SceneEntityAsset> Entities { get; set; } = [];

    [JsonProperty("colliders")]
    public List<SceneColliderAsset> Colliders { get; set; } = [];
}

public sealed class SceneLayerAsset
{
    [JsonProperty("name")]
    public string Name { get; set; } = "layer";

    [JsonProperty("parallax")]
    public float Parallax { get; set; } = 1f;

    [JsonProperty("tilesheet")]
    public string Tilesheet { get; set; } = string.Empty;

    [JsonProperty("tiles")]
    public List<SceneLayerTileAsset> Tiles { get; set; } = [];
}

public sealed class SceneLayerTileAsset
{
    [JsonProperty("tileIndex")]
    public int TileIndex { get; set; }

    [JsonProperty("x")]
    public int X { get; set; }

    [JsonProperty("y")]
    public int Y { get; set; }
}

public sealed class SceneEntityAsset
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("x")]
    public float X { get; set; }

    [JsonProperty("y")]
    public float Y { get; set; }
}

public sealed class SceneColliderAsset
{
    [JsonProperty("x")]
    public float X { get; set; }

    [JsonProperty("y")]
    public float Y { get; set; }

    [JsonProperty("width")]
    public float Width { get; set; }

    [JsonProperty("height")]
    public float Height { get; set; }
}
