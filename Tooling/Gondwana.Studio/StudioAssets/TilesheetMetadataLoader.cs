using System.Drawing;
using Gondwana.Drawing.Tilesheets;
using Newtonsoft.Json;

namespace Gondwana.StudioAssets;

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
    /// LoadAndRegisterTilesheet.
    /// </summary>
    /// <param name="metadataPath">metadataPath.</param>
    /// <returns>The result.</returns>
    public static Tilesheet LoadAndRegisterTilesheet(string metadataPath)
    {
        return null;

        var metadata = Load(metadataPath);
        var metadataDir = Path.GetDirectoryName(metadataPath) ?? string.Empty;
        var imagePath = Path.GetFullPath(Path.Combine(metadataDir, metadata.ImagePath));
        var name = Path.GetFileNameWithoutExtension(metadataPath);

        //var sheet = new Tilesheet(name, imagePath)
        //{
        //    //TileSize = new Size(metadata.TileWidth, metadata.TileHeight)
        //};

        //sheet.ValueBag.Set(new ValueKey<TilesheetMetadataAsset>("gondwana.studio.tilesheet"), metadata);
        //return sheet;
    }
}
