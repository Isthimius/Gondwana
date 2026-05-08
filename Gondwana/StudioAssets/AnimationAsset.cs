using Newtonsoft.Json;

namespace Gondwana.StudioAssets;

/// <summary>
/// AnimationAsset.
/// </summary>
public sealed class AnimationAsset
{
    /// <summary>
    /// Gets or sets the tilesheet path used by the animation.
    /// </summary>
    [JsonProperty("tilesheetPath")]
    public string TilesheetPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the animation name.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cycle type.
    /// </summary>
    [JsonProperty("cycleType")]
    public string CycleType { get; set; } = "Loop";

    /// <summary>
    /// Gets or sets the animation frames.
    /// </summary>
    [JsonProperty("frames")]
    public List<AnimationFrameAsset> Frames { get; set; } = [];
}

/// <summary>
/// AnimationFrameAsset.
/// </summary>
public sealed class AnimationFrameAsset
{
    /// <summary>
    /// Gets or sets the tile index.
    /// </summary>
    [JsonProperty("tileIndex")]
    public int TileIndex { get; set; }

    /// <summary>
    /// Gets or sets the frame duration in milliseconds.
    /// </summary>
    [JsonProperty("durationMs")]
    public int DurationMs { get; set; } = 100;
}
