using Newtonsoft.Json;

namespace Gondwana.StudioAssets;

public sealed class TilesheetMetadataAsset
{
    [JsonProperty("imagePath")]
    public string ImagePath { get; set; } = string.Empty;

    [JsonProperty("tileWidth")]
    public int TileWidth { get; set; } = 16;

    [JsonProperty("tileHeight")]
    public int TileHeight { get; set; } = 16;

    [JsonProperty("tiles")]
    public List<TilesheetTileNameAsset> Tiles { get; set; } = [];
}

public sealed class TilesheetTileNameAsset
{
    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
}
