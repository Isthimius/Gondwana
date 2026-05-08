using Gondwana.Drawing;
using Gondwana.Drawing.Animation;
using Newtonsoft.Json;

namespace Gondwana.StudioAssets;

/// <summary>
/// AnimationAssetLoader.
/// </summary>
public static class AnimationAssetLoader
{
    /// <summary>
    /// Load.
    /// </summary>
    /// <param name="animationPath">animationPath.</param>
    /// <returns>The result.</returns>
    public static AnimationAsset Load(string animationPath)
    {
        var json = File.ReadAllText(animationPath);
        return JsonConvert.DeserializeObject<AnimationAsset>(json)
            ?? throw new InvalidDataException($"Unable to parse animation asset: {animationPath}");
    }

    /// <summary>
    /// ToFrameSequence.
    /// </summary>
    /// <param name="animationPath">animationPath.</param>
    /// <returns>The result.</returns>
    public static FrameSequence ToFrameSequence(string animationPath)
    {
        var asset = Load(animationPath);
        var metadataPath = ResolveRelatedPath(animationPath, asset.TilesheetPath);
        var metadata = TilesheetMetadataLoader.Load(metadataPath);
        var sheet = TilesheetMetadataLoader.LoadAndRegisterTilesheet(metadataPath);
        var xTiles = Math.Max(1, sheet.SkBitmap.Width / Math.Max(1, metadata.TileWidth));

        var frames = new List<Frame>();
        foreach (var frame in asset.Frames)
        {
            var x = frame.TileIndex % xTiles;
            var y = frame.TileIndex / xTiles;
            frames.Add(new Frame(sheet, x, y, Math.Max(1, frame.DurationMs) / 1000d));
        }

        var sequence = new FrameSequence(frames)
        {
            SequenceCycleType = asset.CycleType switch
            {
                "Once" => CycleType.Simple,
                "Loop" => CycleType.Repeating,
                "PingPong" => CycleType.PingPong,
                _ => CycleType.Repeating
            }
        };

        return sequence;
    }

    /// <summary>
    /// ToCycle.
    /// </summary>
    /// <param name="animationPath">animationPath.</param>
    /// <returns>The result.</returns>
    public static Cycle ToCycle(string animationPath)
    {
        var asset = Load(animationPath);
        if (asset.Frames.Count == 0)
            throw new InvalidDataException($"Animation '{asset.Name}' has no frames: {animationPath}");

        var sequence = ToFrameSequence(animationPath);
        var avgDurationMs = asset.Frames.Average(f => Math.Max(1, f.DurationMs));
        var throttleSeconds = avgDurationMs / 1000d;
        return new Cycle(sequence, throttleSeconds, asset.Name);
    }

    private static string ResolveRelatedPath(string ownerPath, string relatedPath)
    {
        var ownerDir = Path.GetDirectoryName(ownerPath) ?? string.Empty;
        return Path.GetFullPath(Path.Combine(ownerDir, relatedPath));
    }
}
