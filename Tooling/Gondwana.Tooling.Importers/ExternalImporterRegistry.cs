namespace Gondwana.Tooling.Importers;

/// <summary>
/// Represents external importer registry.
/// </summary>
public static class ExternalImporterRegistry
{
    /// <summary>
    /// Creates the available external import providers.
    /// </summary>
    /// <returns>The available external asset import providers.</returns>
    public static IReadOnlyList<IExternalAssetImporter> CreateProviders() =>
        [new GodotTilesetImporter(), new TiledTilesetImporter(), new TiledMapImporter(), new AsepriteImporter()];
}
