using Gondwana.Drawing.Tilesheets;
using Newtonsoft.Json;

namespace Gondwana.Tooling.StudioAssets;

/// <summary>
/// TilesheetMetadataLoader.
/// </summary>
public static class TilesheetMetadataLoader
{
    /// <summary>
    /// Load.
    /// </summary>
    /// <param name="metadataPath">metadataPath.</param>
    /// <returns>The result.</returns>
    public static TilesheetMetadataAsset Load(string metadataPath)
    {
        var json = File.ReadAllText(metadataPath);
        return JsonConvert.DeserializeObject<TilesheetMetadataAsset>(json)
            ?? throw new InvalidDataException($"Unable to parse tilesheet metadata: {metadataPath}");
    }

    /// <summary>
    /// Loads and registers a tilesheet for use in the engine.
    /// </summary>
    /// <param name="metadataPath">metadataPath.</param>
    /// <returns>The registered tilesheet.</returns>
    public static Tilesheet LoadAndRegisterTilesheet(string metadataPath)
    {
        throw new NotImplementedException("LoadAndRegisterTilesheet is not implemented in Gondwana.Tooling.Studio.Core. Override this in a platform-specific subclass or provide an engine integration.");
    }
}
