using Gondwana.Drawing;
using Gondwana.Scenes;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Gondwana.StudioAssets;

/// <summary>
/// SceneLoader.
/// </summary>
public static class SceneLoader
{
    /// <summary>
    /// LoadAsset.
    /// </summary>
    /// <param name="scenePath">scenePath.</param>
    /// <returns>The result.</returns>
    public static SceneAsset LoadAsset(string scenePath)
    {
        var json = File.ReadAllText(scenePath);
        return JsonConvert.DeserializeObject<SceneAsset>(json)
            ?? throw new InvalidDataException($"Unable to parse scene asset: {scenePath}");
    }

    /// <summary>
    /// LoadScene.
    /// </summary>
    /// <param name="scenePath">scenePath.</param>
    /// <returns>The result.</returns>
    public static Scene LoadScene(string scenePath)
    {
        var sceneAsset = LoadAsset(scenePath);
        var scene = new Scene();

        foreach (var layerAsset in sceneAsset.Layers)
        {
            var tilesheetMetadataPath = ResolveRelatedPath(scenePath, layerAsset.Tilesheet);
            var metadata = TilesheetMetadataLoader.Load(tilesheetMetadataPath);
            var tilesheet = TilesheetMetadataLoader.LoadAndRegisterTilesheet(tilesheetMetadataPath);
            var xTiles = Math.Max(1, tilesheet.SkBitmap.Width / Math.Max(1, metadata.TileWidth));

            var maxX = layerAsset.Tiles.Count == 0 ? 1 : layerAsset.Tiles.Max(t => t.X + 1);
            var maxY = layerAsset.Tiles.Count == 0 ? 1 : layerAsset.Tiles.Max(t => t.Y + 1);
            var layer = scene.AddLayer(maxX, maxY, metadata.TileWidth, metadata.TileHeight, 0, layerAsset.Parallax);
            //layer.ID = layerAsset.Name;

            foreach (var tile in layerAsset.Tiles)
            {
                var x = tile.TileIndex % xTiles;
                var y = tile.TileIndex / xTiles;
                var layerTile = layer[tile.X, tile.Y];
                if (layerTile is null)
                {
                    Engine.Logger.LogWarning(
                        "Skipping out-of-bounds scene tile at ({TileX}, {TileY}) in layer '{LayerName}' with size {Width}x{Height}.",
                        tile.X,
                        tile.Y,
                        layerAsset.Name,
                        maxX,
                        maxY);
                    continue;
                }

                //layerTile.CurrentFrame = new Frame(tilesheet, x, y);
            }
        }

        if (sceneAsset.Entities.Count > 0)
            scene.ValueBag.Set(new ValueKey<List<SceneEntityAsset>>("gondwana.studio.scene.entities"), sceneAsset.Entities);

        if (sceneAsset.Colliders.Count > 0)
            scene.ValueBag.Set(new ValueKey<List<SceneColliderAsset>>("gondwana.studio.scene.colliders"), sceneAsset.Colliders);

        return scene;
    }

    private static string ResolveRelatedPath(string ownerPath, string relatedPath)
    {
        var ownerDir = Path.GetDirectoryName(ownerPath) ?? string.Empty;
        return Path.GetFullPath(Path.Combine(ownerDir, relatedPath));
    }
}
