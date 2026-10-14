using Gondwana.Drawing.Animation.GANI;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.Scenes.Editing;

/// <summary>
/// Loose GANI definition loaded for GSCN animation assignment and preview.
/// </summary>
internal sealed class SceneAnimationSource : IDisposable
{
    private readonly List<SceneTilesheetSource> _tilesheets = [];

    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string FilePath { get; }
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

    private SceneAnimationSource(string filePath, AnimationDefinition definition)
    {
        FilePath = filePath;
        BaseDirectory = Path.GetDirectoryName(filePath)!;
        Definition = definition;
        LoadTilesheetDependencies();
    }

    /// <summary>
    /// Loads scene animation source from the supplied source.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The resulting scene animation source.</returns>
    /// <param name="identity">The logical member identity; null requires a singleton.</param>
    public static SceneAnimationSource LoadMember(string path, string? identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = Path.GetFullPath(path);
        return new(path, AnimationDefinitionSerializer.Load(path, identity));
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

    private void LoadTilesheetDependencies()
    {
        foreach (var source in Definition.TilesheetSources ?? [])
        {
            if (source.Kind != AnimationTilesheetSourceKind.LooseDefinitionFile ||
                string.IsNullOrWhiteSpace(source.GtsPath))
            {
                _diagnostics.Add(
                    $"INFO: GANI tilesheet '{source.Tilesheet}' uses a packed source; preview is not available yet.");
                continue;
            }

            try
            {
                string path = Path.IsPathRooted(source.GtsPath)
                    ? Path.GetFullPath(source.GtsPath)
                    : Path.GetFullPath(source.GtsPath, BaseDirectory);

                var loaded = SceneTilesheetSource.Load(path);
                if (!string.Equals(
                        loaded.Definition.Name,
                        source.Tilesheet,
                        StringComparison.Ordinal))
                {
                    _diagnostics.Add(
                        $"WARNING: GANI source '{source.Tilesheet}' resolves to GTS '{loaded.Definition.Name}'.");
                }

                _tilesheets.Add(loaded);
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
    public static IReadOnlyList<SceneAnimationSource> LoadAll(string path) =>
        AnimationDefinitionSerializer.LoadAll(path).Select(definition => new SceneAnimationSource(Path.GetFullPath(path), definition)).ToList();

    /// <summary>Loads one logical source from a packed definition document.</summary>
    /// <param name="archivePath">The containing archive path.</param>
    /// <param name="entry">The physical entry name.</param>
    /// <param name="identity">The logical member identity.</param>
    /// <returns>The selected source.</returns>
    public static SceneAnimationSource LoadPacked(string archivePath, string entry, string identity)
    {
        archivePath = Path.GetFullPath(archivePath);
        using var archive = Gondwana.Assets.AssetsFile.LoadOrCreate(archivePath, null, false, register: false);
        return new(archivePath, AnimationDefinitionSerializer.Load(archive, entry, identity));
    }
}
