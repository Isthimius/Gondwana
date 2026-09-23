namespace Gondwana.Tooling.Importers;

public static class ExternalImporterRegistry
{
    public static IReadOnlyList<IExternalAssetImporter> CreateProviders() =>
        [new GodotTilesetImporter(), new TiledTilesetImporter(), new TiledMapImporter(), new AsepriteImporter()];
}
