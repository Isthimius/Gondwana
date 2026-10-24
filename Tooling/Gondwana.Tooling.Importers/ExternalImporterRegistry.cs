namespace Gondwana.Tooling.Importers;

/// <summary>Supplies the built-in format providers used by Studio and headless callers.</summary>
public static class ExternalImporterRegistry
{
    /// <summary>Creates fresh Godot TileSet, Tiled tileset, Tiled map, and Aseprite providers in auto-detection order.</summary>
    /// <returns>A new provider list; no shared provider instances are retained by the registry.</returns>
    public static IReadOnlyList<IExternalAssetImporter> CreateProviders() =>
        [new GodotTilesetImporter(), new TiledTilesetImporter(), new TiledMapImporter(), new AsepriteImporter()];
}
