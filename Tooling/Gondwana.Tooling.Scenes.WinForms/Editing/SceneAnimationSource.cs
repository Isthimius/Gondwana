using Gondwana.Drawing.Animation.GANI;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.Scenes.Editing;

/// <summary>
/// Loose GANI definition loaded for GSCN animation assignment and preview.
/// </summary>
internal sealed class SceneAnimationSource : IDisposable
{
    private readonly List<SceneTilesheetSource> _tilesheets = [];

    public string FilePath { get; }
    public string BaseDirectory { get; }
    public AnimationDefinition Definition { get; }
    public IReadOnlyList<SceneTilesheetSource> Tilesheets => _tilesheets;
    public IReadOnlyList<string> Diagnostics => _diagnostics;
    private readonly List<string> _diagnostics = [];

    private SceneAnimationSource(string filePath, AnimationDefinition definition)
    {
        FilePath = filePath;
        BaseDirectory = Path.GetDirectoryName(filePath)!;
        Definition = definition;
        LoadTilesheetDependencies();
    }

    public static SceneAnimationSource Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = Path.GetFullPath(path);
        return new(path, AnimationDefinitionSerializer.Load(path));
    }

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

    public void Dispose()
    {
        foreach (var tilesheet in _tilesheets)
            tilesheet.Dispose();
        _tilesheets.Clear();
    }
}
