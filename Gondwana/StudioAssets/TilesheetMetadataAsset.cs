using Newtonsoft.Json;

namespace Gondwana.StudioAssets;

/// <summary>
/// TilesheetMetadataAsset.
/// </summary>
public sealed class TilesheetMetadataAsset
{
    /// <summary>
    /// Gets or sets the source image path.
    /// </summary>
    [JsonProperty("imagePath")]
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tile width.
    /// </summary>
    [JsonProperty("tileWidth")]
    public int TileWidth { get; set; } = 16;

    /// <summary>
    /// Gets or sets the tile height.
    /// </summary>
    [JsonProperty("tileHeight")]
    public int TileHeight { get; set; } = 16;

    /// <summary>
    /// Gets or sets the named tile list.
    /// </summary>
    [JsonProperty("tiles")]
    public List<TilesheetTileNameAsset> Tiles { get; set; } = [];
}

/// <summary>
/// TilesheetTileNameAsset.
/// </summary>
public sealed class TilesheetTileNameAsset
{
    /// <summary>
    /// Gets or sets the tile index.
    /// </summary>
    [JsonProperty("index")]
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the tile name.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
}
