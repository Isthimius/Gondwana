using System.Drawing;
using Gondwana.Drawing.Tilesheets;
using Newtonsoft.Json;

namespace Gondwana.StudioAssets;

public static class TilesheetMetadataLoader
{
    public static TilesheetMetadataAsset Load(string metadataPath)
    {
        var json = File.ReadAllText(metadataPath);
        return JsonConvert.DeserializeObject<TilesheetMetadataAsset>(json)
            ?? throw new InvalidDataException($"Unable to parse tilesheet metadata: {metadataPath}");
    }

    public static Tilesheet LoadAndRegisterTilesheet(string metadataPath)
    {
        var metadata = Load(metadataPath);
        var metadataDir = Path.GetDirectoryName(metadataPath) ?? string.Empty;
        var imagePath = Path.GetFullPath(Path.Combine(metadataDir, metadata.ImagePath));
        var name = Path.GetFileNameWithoutExtension(metadataPath);

        var sheet = new Tilesheet(name, imagePath)
        {
            TileSize = new Size(metadata.TileWidth, metadata.TileHeight)
        };

        sheet.ValueBag.Set(new ValueKey<TilesheetMetadataAsset>("gondwana.studio.tilesheet"), metadata);
        return sheet;
    }
}
