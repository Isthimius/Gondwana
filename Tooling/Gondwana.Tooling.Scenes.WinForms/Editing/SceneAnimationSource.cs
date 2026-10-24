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

    /// <summary>Gets the full loose document path or containing archive path; use Resource for packed entry identity.</summary>
    public string FilePath => Resource.Path;
    /// <summary>The physical source identity, separate from the animation's logical key.</summary>
    public AuthoringResource Resource { get; }
    /// <summary>Gets the full directory containing the document or archive, used to resolve its dependencies.</summary>
    public string BaseDirectory { get; }
    /// <summary>Gets the selected deserialized GANI member used by the authoring preview.</summary>
    public AnimationDefinition Definition { get; }
    /// <summary>Gets successfully loaded explicit tilesheet dependencies owned by this animation source.</summary>
    public IReadOnlyList<SceneTilesheetSource> Tilesheets => _tilesheets;
    /// <summary>Gets dependency-load and preview warnings collected while constructing this source.</summary>
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

    /// <summary>Loads a logical member from a loose GANI file and prepares its authoring preview dependencies.</summary>
    /// <param name="path">Loose document path, normalized against the process current directory.</param>
    /// <param name="identity">Logical member identity, or null to require a singleton document.</param>
    /// <param name="packages">Borrowed package cache, or null to use a temporary cache during loading.</param>
    /// <returns>A source owned by the caller, which must dispose its preview resources.</returns>
    public static SceneAnimationSource LoadMember(string path, string? identity, AssetPackageCatalog? packages = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = Path.GetFullPath(path);
        return new(new AuthoringResource(path, Gondwana.Assets.AssetTypes.AnimationDefinition),
            AnimationDefinitionSerializer.Load(path, identity), packages);
    }

    /// <summary>Copies the first animation frame's tilesheet and grid reference without resolving its image.</summary>
    /// <returns>A new scene frame reference, or null if the animation has no frames.</returns>
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

    /// <summary>Finds the first loaded tilesheet with an exact case-sensitive logical name.</summary>
    /// <param name="logicalName">Tilesheet definition name to match.</param>
    /// <returns>A borrowed source owned by this animation source, or null when absent.</returns>
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

    /// <summary>Disposes every loaded tilesheet preview and clears the dependency source list.</summary>
    public void Dispose()
    {
        foreach (var tilesheet in _tilesheets)
            tilesheet.Dispose();
        _tilesheets.Clear();
    }

    /// <summary>Loads a singleton loose GANI document with its preview resources.</summary>
    /// <param name="path">Loose document path.</param>
    /// <returns>A source owned by the caller, which must dispose it.</returns>
    public static SceneAnimationSource Load(string path) => LoadMember(path, null);

    /// <summary>Loads all GANI members from a loose file in document order and prepares their previews.</summary>
    /// <param name="path">Loose document path.</param>
    /// <param name="packages">Borrowed package cache, or null to create temporary caches during loading.</param>
    /// <returns>Independent source objects; the caller must dispose every returned source.</returns>
    public static IReadOnlyList<SceneAnimationSource> LoadAll(string path, AssetPackageCatalog? packages = null) =>
        AnimationDefinitionSerializer.LoadAll(path).Select(definition => new SceneAnimationSource(
            new AuthoringResource(Path.GetFullPath(path), Gondwana.Assets.AssetTypes.AnimationDefinition), definition, packages)).ToList();

    /// <summary>Loads one logical member from an exact typed packed GANI entry.</summary>
    /// <param name="archivePath">Containing archive path, normalized against the process current directory.</param>
    /// <param name="entry">Exact entry name within the expected definition asset type.</param>
    /// <param name="identity">Logical member identity within the packed document.</param>
    /// <param name="packages">Borrowed package cache, or null to use a temporary cache during loading.</param>
    /// <returns>A source owned by the caller, which must dispose its preview resources.</returns>
    public static SceneAnimationSource LoadPacked(string archivePath, string entry, string identity, AssetPackageCatalog? packages = null)
    {
        archivePath = Path.GetFullPath(archivePath);
        using var owned = packages is null ? new AssetPackageCatalog() : null;
        packages ??= owned!;
        var resource = new AuthoringResource(archivePath, Gondwana.Assets.AssetTypes.AnimationDefinition, entry);
        return new(resource, new AuthoringResourceResolver(packages).LoadAnimation(resource, identity), packages);
    }
}
