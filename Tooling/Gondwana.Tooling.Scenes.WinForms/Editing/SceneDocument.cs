using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.Scenes.Editing;

/// <summary>
/// UI-independent editing session for one GSCN scene definition.
/// </summary>
public sealed class SceneDocument
{
    public SceneDefinition Definition { get; }
    public string? FilePath { get; private set; }
    public string BaseDirectory { get; private set; }
    public bool IsDirty { get; private set; }
    public event EventHandler? Changed;

    private SceneDocument(
        SceneDefinition definition,
        string? filePath,
        string baseDirectory,
        bool isDirty)
    {
        Definition = definition;
        FilePath = filePath;
        BaseDirectory = Path.GetFullPath(baseDirectory);
        IsDirty = isDirty;
    }

    public static SceneDocument Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = Path.GetFullPath(path);
        return new(
            SceneDefinitionSerializer.Load(path),
            path,
            Path.GetDirectoryName(path)!,
            isDirty: false);
    }

    public static SceneDocument Create(string directory) =>
        new(
            new SceneDefinition
            {
                ID = "scene",
                Source = SceneDefinitionSource.Generated()
            },
            filePath: null,
            directory,
            isDirty: true);

    public SceneLayerDefinition AddLayer()
    {
        var layer = new SceneLayerDefinition
        {
            ID = UniqueLayerId(),
            Columns = 10,
            Rows = 10,
            TileWidth = 32,
            TileHeight = 32,
            ZOrder = Definition.Layers.Count
        };
        Definition.Layers.Add(layer);
        MarkChanged();
        return layer;
    }

    public bool RemoveLayer(SceneLayerDefinition layer)
    {
        if (!Definition.Layers.Remove(layer))
            return false;
        MarkChanged();
        return true;
    }

    public SceneLayerTileDefinition? FindTile(
        SceneLayerDefinition layer,
        int x,
        int y) =>
        layer.Tiles.FirstOrDefault(tile =>
            tile.X == x &&
            tile.Y == y);

    public SceneLayerTileDefinition GetOrCreateTile(
        SceneLayerDefinition layer,
        int x,
        int y)
    {
        if (x < 0 || y < 0 || x >= layer.Columns || y >= layer.Rows)
            throw new ArgumentOutOfRangeException(nameof(x), "Tile coordinates must be inside the selected layer.");

        var existing = FindTile(layer, x, y);
        if (existing is not null)
            return existing;

        var tile = new SceneLayerTileDefinition
        {
            X = x,
            Y = y
        };
        layer.Tiles.Add(tile);
        MarkChanged();
        return tile;
    }

    public bool RemoveTile(SceneLayerDefinition layer, int x, int y)
    {
        int removed = layer.Tiles.RemoveAll(tile =>
            tile.X == x &&
            tile.Y == y);
        if (removed == 0)
            return false;
        MarkChanged();
        return true;
    }

    public bool SetLooseTilesheetSource(string tilesheet, string gtsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tilesheet);
        ArgumentException.ThrowIfNullOrWhiteSpace(gtsPath);
        string fullPath = Path.GetFullPath(gtsPath);
        string persisted = MakeReferencePath(fullPath, BaseDirectory);

        var existing = Definition.TilesheetSources.FirstOrDefault(source =>
            string.Equals(source.Tilesheet, tilesheet, StringComparison.Ordinal));

        if (existing is not null &&
            existing.Kind == SceneTilesheetSourceKind.LooseDefinitionFile &&
            !string.IsNullOrWhiteSpace(existing.GtsPath) &&
            string.Equals(
                ResolveReferencePath(existing.GtsPath),
                fullPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Definition.TilesheetSources.RemoveAll(source =>
            string.Equals(source.Tilesheet, tilesheet, StringComparison.Ordinal));
        Definition.TilesheetSources.Add(
            SceneTilesheetSourceDefinition.Loose(tilesheet, persisted));
        MarkChanged();
        return true;
    }

    public bool SetLooseAnimationSource(string animationKey, string ganiPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(animationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(ganiPath);
        string fullPath = Path.GetFullPath(ganiPath);
        string persisted = MakeReferencePath(fullPath, BaseDirectory);

        var existing = Definition.AnimationSources.FirstOrDefault(source =>
            string.Equals(source.AnimationKey, animationKey, StringComparison.Ordinal));

        if (existing is not null &&
            existing.Kind == SceneAnimationSourceKind.LooseDefinitionFile &&
            !string.IsNullOrWhiteSpace(existing.GaniPath) &&
            string.Equals(
                ResolveReferencePath(existing.GaniPath),
                fullPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Definition.AnimationSources.RemoveAll(source =>
            string.Equals(source.AnimationKey, animationKey, StringComparison.Ordinal));
        Definition.AnimationSources.Add(
            SceneAnimationSourceDefinition.Loose(animationKey, persisted));
        MarkChanged();
        return true;
    }

    public bool RemoveTilesheetSource(string tilesheet)
    {
        int removed = Definition.TilesheetSources.RemoveAll(source =>
            string.Equals(source.Tilesheet, tilesheet, StringComparison.Ordinal));
        if (removed == 0)
            return false;
        MarkChanged();
        return true;
    }

    public bool RemoveAnimationSource(string animationKey)
    {
        int removed = Definition.AnimationSources.RemoveAll(source =>
            string.Equals(source.AnimationKey, animationKey, StringComparison.Ordinal));
        if (removed == 0)
            return false;
        MarkChanged();
        return true;
    }

    public string ResolveReferencePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ResolveReferencePath(path, BaseDirectory);
    }

    public void MarkChanged()
    {
        IsDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<string> Validate() =>
        SceneDefinitionValidator.Validate(Definition);

    public void Save(string path, bool allowInvalid = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var errors = Validate();
        if (errors.Count > 0 && !allowInvalid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

        path = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        var snapshot = SceneDefinitionSerializer.FromJson(
            SceneDefinitionSerializer.ToJson(Definition));

        RebaseSources(snapshot, BaseDirectory, directory);

        var temporary = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            SceneDefinitionSerializer.Save(temporary, snapshot);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }

        var saved = SceneDefinitionSerializer.Load(path);
        Definition.ID = saved.ID;
        Definition.CollisionGroups = saved.CollisionGroups;
        Definition.CollisionProfiles = saved.CollisionProfiles;
        Definition.TilesheetSources = saved.TilesheetSources;
        Definition.AnimationSources = saved.AnimationSources;
        Definition.Layers = saved.Layers;
        Definition.Source = saved.Source;

        FilePath = path;
        BaseDirectory = directory;
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private string UniqueLayerId()
    {
        int n = Definition.Layers.Count + 1;
        string candidate;
        do candidate = $"layer-{n++}";
        while (Definition.Layers.Any(layer =>
            string.Equals(layer.ID, candidate, StringComparison.Ordinal)));
        return candidate;
    }

    private static void RebaseSources(
        SceneDefinition definition,
        string oldBaseDirectory,
        string newBaseDirectory)
    {
        foreach (var source in definition.TilesheetSources)
        {
            switch (source.Kind)
            {
                case SceneTilesheetSourceKind.LooseDefinitionFile
                    when !string.IsNullOrWhiteSpace(source.GtsPath):
                    source.GtsPath = MakeReferencePath(
                        ResolveReferencePath(source.GtsPath, oldBaseDirectory),
                        newBaseDirectory);
                    break;

                case SceneTilesheetSourceKind.PackedDefinitionFile
                    when !string.IsNullOrWhiteSpace(source.AssetsFilePath):
                    source.AssetsFilePath = MakeReferencePath(
                        ResolveReferencePath(source.AssetsFilePath, oldBaseDirectory),
                        newBaseDirectory);
                    break;
            }
        }

        foreach (var source in definition.AnimationSources)
        {
            switch (source.Kind)
            {
                case SceneAnimationSourceKind.LooseDefinitionFile
                    when !string.IsNullOrWhiteSpace(source.GaniPath):
                    source.GaniPath = MakeReferencePath(
                        ResolveReferencePath(source.GaniPath, oldBaseDirectory),
                        newBaseDirectory);
                    break;

                case SceneAnimationSourceKind.PackedDefinitionFile
                    when !string.IsNullOrWhiteSpace(source.AssetsFilePath):
                    source.AssetsFilePath = MakeReferencePath(
                        ResolveReferencePath(source.AssetsFilePath, oldBaseDirectory),
                        newBaseDirectory);
                    break;
            }
        }
    }

    private static string ResolveReferencePath(
        string path,
        string baseDirectory) =>
        Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(path, baseDirectory);

    private static string MakeReferencePath(
        string path,
        string baseDirectory)
    {
        string fullPath = Path.GetFullPath(path);
        string fullBase = Path.GetFullPath(baseDirectory);
        if (!string.Equals(
                Path.GetPathRoot(fullPath),
                Path.GetPathRoot(fullBase),
                StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        return Path.GetRelativePath(fullBase, fullPath);
    }
}
