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

    private SceneDocument(SceneDefinition definition, string? filePath, string baseDirectory, bool isDirty)
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
        SceneDefinitionSerializer.Save(path, Definition);
        var saved = SceneDefinitionSerializer.Load(path);

        Definition.ID = saved.ID;
        Definition.CollisionGroups = saved.CollisionGroups;
        Definition.CollisionProfiles = saved.CollisionProfiles;
        Definition.TilesheetSources = saved.TilesheetSources;
        Definition.AnimationSources = saved.AnimationSources;
        Definition.Layers = saved.Layers;
        Definition.Source = saved.Source;

        FilePath = path;
        BaseDirectory = Path.GetDirectoryName(path)!;
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
}
