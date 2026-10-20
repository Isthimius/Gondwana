using Gondwana.Drawing.Animation.GANI;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Tilesheets.Sources;

namespace Gondwana.Tooling.Scenes.Editing;

/// <summary>
/// Loose or packed GANI definition loaded for GSCN animation assignment and preview.
/// </summary>
internal sealed class SceneAnimationSource : IDisposable
{
    private readonly List<SceneTilesheetSource> _tilesheets = [];

    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string FilePath => Resource.Path;
    /// <summary>The physical source identity, separate from the animation's logical key.</summary>
    public AuthoringResource Resource { get; }
    /// <summary>
    /// Gets the base directory.
    /// </summary>
    public string BaseDirectory { get; }
    /// <summary>
    /// Gets the definition.
    /// </summary>
    public AnimationDefinition Definition { get; }
    /// <summary>
    /// Gets the tilesheets.
    /// </summary>
    public IReadOnlyList<SceneTilesheetSource> Tilesheets => _tilesheets;
    /// <summary>
    /// Gets the diagnostics.
    /// </summary>
    public IReadOnlyList<string> Diagnostics => _diagnostics;
    private readonly List<string> _diagnostics = [];

    private SceneAnimationSource(AuthoringResource resource, AnimationDefinition definition, AssetPackageCatalog? packages = null)
    {
        Resource = resource;
        BaseDirectory = resource.BaseDirectory;
        Definition = definition;
        using var owned = packages is null ? new AssetPackageCatalog() : null;
        LoadTilesheetDependencies(packages ?? owned!);
    }

    /// <summary>
    /// Loads scene animation source from the supplied source.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The resulting scene animation source.</returns>
    /// <param name="identity">The logical member identity; null requires a singleton.</param>
    /// <param name="packages">Optional shared authoring package catalog.</param>
    public static SceneAnimationSource LoadMember(string path, string? identity, AssetPackageCatalog? packages = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = Path.GetFullPath(path);
        return new(new AuthoringResource(path, Gondwana.Assets.AssetTypes.AnimationDefinition),
            AnimationDefinitionSerializer.Load(path, identity), packages);
    }

    /// <summary>
    /// Gets the first resolvable frame for the animation preview.
    /// </summary>
    /// <returns>The first resolvable animation frame, or <see langword="null"/> if none is available.</returns>
    public SceneFrameDefinition? FirstPreviewFrame()
    {
        var frame = Definition.Frames.FirstOrDefault();
        if (frame is null)
            return null;

        return new SceneFrameDefinition
        {
            Tilesheet = frame.Tilesheet,
            RegionName = frame.RegionName,
            XTile = frame.XTile,
            YTile = frame.YTile
        };
    }

    /// <summary>
    /// Finds the tilesheet source registered under the requested logical name.
    /// </summary>
    /// <param name="logicalName">The logical name.</param>
    /// <returns>The requested scene tilesheet source, or <see langword="null"/> when unavailable.</returns>
    public SceneTilesheetSource? FindTilesheet(string logicalName) =>
        _tilesheets.FirstOrDefault(source =>
            string.Equals(source.Definition.Name, logicalName, StringComparison.Ordinal));

    private void LoadTilesheetDependencies(AssetPackageCatalog packages)
    {
        foreach (var source in Definition.TilesheetSources ?? [])
        {
            try
            {
                var resource = AuthoringResourceResolver.TilesheetSource(source, BaseDirectory);
                var loaded = SceneTilesheetSource.LoadResource(resource, source.Tilesheet, packages);
                if (!string.Equals(
                        loaded.Definition.Name,
                        source.Tilesheet,
                        StringComparison.Ordinal))
                {
                    _diagnostics.Add(
                        $"WARNING: GANI source '{source.Tilesheet}' resolves to GTS '{loaded.Definition.Name}'.");
                }

                _tilesheets.Add(loaded);
                if (loaded.PreviewWarning is { } warning)
                    _diagnostics.Add($"WARNING: GANI tilesheet '{source.Tilesheet}': {warning}");
            }
            catch (Exception ex) when (
                ex is IOException or
                InvalidDataException or
                ArgumentException or
                UnauthorizedAccessException or
                InvalidOperationException or
                NotSupportedException)
            {
                _diagnostics.Add(
                    $"WARNING: Could not load GANI tilesheet '{source.Tilesheet}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Releases the resources owned by this instance.
    /// </summary>
    public void Dispose()
    {
        foreach (var tilesheet in _tilesheets)
            tilesheet.Dispose();
        _tilesheets.Clear();
    }

    /// <summary>Loads a singleton source document.</summary>
    /// <param name="path">The physical document path.</param>
    /// <returns>The source.</returns>
    public static SceneAnimationSource Load(string path) => LoadMember(path, null);

    /// <summary>Loads every member for authoring source selection.</summary>
    /// <param name="path">The physical document path.</param>
    /// <returns>All logical sources.</returns>
    /// <param name="packages">Optional shared authoring package catalog.</param>
    public static IReadOnlyList<SceneAnimationSource> LoadAll(string path, AssetPackageCatalog? packages = null) =>
        AnimationDefinitionSerializer.LoadAll(path).Select(definition => new SceneAnimationSource(
            new AuthoringResource(Path.GetFullPath(path), Gondwana.Assets.AssetTypes.AnimationDefinition), definition, packages)).ToList();

    /// <summary>Loads one logical source from a packed definition document.</summary>
    /// <param name="archivePath">The containing archive path.</param>
    /// <param name="entry">The physical entry name.</param>
    /// <param name="identity">The logical member identity.</param>
    /// <returns>The selected source.</returns>
    /// <param name="packages">Optional shared authoring package catalog.</param>
    public static SceneAnimationSource LoadPacked(string archivePath, string entry, string identity, AssetPackageCatalog? packages = null)
    {
        archivePath = Path.GetFullPath(archivePath);
        using var owned = packages is null ? new AssetPackageCatalog() : null;
        packages ??= owned!;
        var resource = new AuthoringResource(archivePath, Gondwana.Assets.AssetTypes.AnimationDefinition, entry);
        return new(resource, new AuthoringResourceResolver(packages).LoadAnimation(resource, identity), packages);
    }
}
