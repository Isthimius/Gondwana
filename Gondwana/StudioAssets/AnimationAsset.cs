using Newtonsoft.Json;

namespace Gondwana.StudioAssets;

public sealed class AnimationAsset
{
    [JsonProperty("tilesheetPath")]
    public string TilesheetPath { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("cycleType")]
    public string CycleType { get; set; } = "Loop";

    [JsonProperty("frames")]
    public List<AnimationFrameAsset> Frames { get; set; } = [];
}

public sealed class AnimationFrameAsset
{
    [JsonProperty("tileIndex")]
    public int TileIndex { get; set; }

    [JsonProperty("durationMs")]
    public int DurationMs { get; set; } = 100;
}
