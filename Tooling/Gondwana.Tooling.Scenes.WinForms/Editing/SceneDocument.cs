using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Scenes.Editing;

/// <summary>
/// UI-independent editing session for one GSCN scene definition.
/// </summary>
public sealed partial class SceneDocument
{
    /// <summary>
    /// Gets the definition.
    /// </summary>
    public SceneDefinition Definition { get; private set; }
    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string? FilePath { get; private set; }
    /// <summary>
    /// Gets the base directory.
    /// </summary>
    public string BaseDirectory { get; private set; }
    /// <summary>
    /// Gets whether the document contains unsaved changes.
    /// </summary>
    public bool IsDirty { get; private set; }
    /// <summary>
    /// Occurs when the document content changes.
    /// </summary>
    public event EventHandler? Changed;

    private SceneDocument(
        SceneDefinition definition,
        string? filePath,
        string baseDirectory,
        bool isDirty)
    {
        _definitions = [definition];
        Definition = definition;
        FilePath = filePath;
        BaseDirectory = Path.GetFullPath(baseDirectory);
        IsDirty = isDirty;
    }

    /// <summary>
    /// Opens the supplied file for use by the scene document.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The resulting scene document.</returns>
    public static SceneDocument Open(string path)
    {
        path = Path.GetFullPath(path);
        var definitions = SceneDefinitionSerializer.LoadAll(path).ToList();
        if (definitions.Count == 0) throw new InvalidDataException("Document contains no definitions.");
        var document = new SceneDocument(definitions[0], path, Path.GetDirectoryName(path)!, false);
        document._definitions = definitions;
        return document;
    }

    /// <summary>
    /// Creates a new scene document.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The resulting scene document.</returns>
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

    /// <summary>
    /// Adds a new layer to the scene definition.
    /// </summary>
    /// <returns>The new layer definition added to the document.</returns>
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

    /// <summary>
    /// Removes the selected layer from the scene definition.
    /// </summary>
    /// <param name="layer">The layer.</param>
    /// <returns><see langword="true"/> if the entry was found and removed; otherwise, <see langword="false"/>.</returns>
    public bool RemoveLayer(SceneLayerDefinition layer)
    {
        if (!Definition.Layers.Remove(layer))
            return false;
        MarkChanged();
        return true;
    }

    /// <summary>
    /// Finds an explicitly stored tile at the requested layer coordinates.
    /// </summary>
    /// <param name="layer">The layer.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <returns>The requested scene layer tile definition, or <see langword="null"/> when unavailable.</returns>
    public SceneLayerTileDefinition? FindTile(
        SceneLayerDefinition layer,
        int x,
        int y) =>
        layer.Tiles.FirstOrDefault(tile =>
            tile.X == x &&
            tile.Y == y);

    /// <summary>
    /// Gets the tile definition at the requested coordinates, creating it if necessary.
    /// </summary>
    /// <param name="layer">The layer.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <returns>The existing tile definition, or a newly added definition at those coordinates.</returns>
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

    /// <summary>
    /// Removes the stored tile definition at the requested coordinates.
    /// </summary>
    /// <param name="layer">The layer.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <returns><see langword="true"/> if the entry was found and removed; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Adds or updates a tilesheet reference to a loose GTS file.
    /// </summary>
    /// <param name="tilesheet">The tilesheet.</param>
    /// <param name="gtsPath">The path to the loose GTS tilesheet definition.</param>
    /// <returns><see langword="true"/> if the source reference changed; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Adds or updates an animation reference to a loose GANI file.
    /// </summary>
    /// <param name="animationKey">The lookup key of the animation.</param>
    /// <param name="ganiPath">The path to the loose GANI animation definition.</param>
    /// <returns><see langword="true"/> if the source reference changed; otherwise, <see langword="false"/>.</returns>
    public bool SetLooseAnimationSource(string animationKey, string ganiPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(animationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(ganiPath);
        string fullPath = Path.GetFullPath(ganiPath);
        string persisted = MakeReferencePath(fullPath, BaseDirectory);

        var existing = Definition.AnimationSources.FirstOrDefault(source =>
            string.Equals(source.AnimationKey, animationKey, StringComparison.OrdinalIgnoreCase));

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
            string.Equals(source.AnimationKey, animationKey, StringComparison.OrdinalIgnoreCase));
        Definition.AnimationSources.Add(
            SceneAnimationSourceDefinition.Loose(animationKey, persisted));
        MarkChanged();
        return true;
    }

    /// <summary>
    /// Removes the named tilesheet source from the document.
    /// </summary>
    /// <param name="tilesheet">The tilesheet.</param>
    /// <returns><see langword="true"/> if the entry was found and removed; otherwise, <see langword="false"/>.</returns>
    public bool RemoveTilesheetSource(string tilesheet)
    {
        int removed = Definition.TilesheetSources.RemoveAll(source =>
            string.Equals(source.Tilesheet, tilesheet, StringComparison.Ordinal));
        if (removed == 0)
            return false;
        MarkChanged();
        return true;
    }

    /// <summary>
    /// Removes the named animation source from the document.
    /// </summary>
    /// <param name="animationKey">The lookup key of the animation.</param>
    /// <returns><see langword="true"/> if the entry was found and removed; otherwise, <see langword="false"/>.</returns>
    public bool RemoveAnimationSource(string animationKey)
    {
        int removed = Definition.AnimationSources.RemoveAll(source =>
            string.Equals(source.AnimationKey, animationKey, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
            return false;
        MarkChanged();
        return true;
    }

    /// <summary>
    /// Resolves a document-relative resource path to an absolute path.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The absolute path of the referenced resource.</returns>
    public string ResolveReferencePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ResolveReferencePath(path, BaseDirectory);
    }

    /// <summary>
    /// Marks the document as modified and notifies listeners.
    /// </summary>
    public void MarkChanged()
    {
        IsDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Checks the definition and collects validation errors.
    /// </summary>
    /// <returns>The validation errors; an empty collection indicates that validation passed.</returns>
    public IReadOnlyList<string> Validate() =>
        SceneDefinitionSerializer.ValidateDocument(new SceneDefinitionDocument { Definitions = _definitions });

    /// <summary>
    /// Saves the current content to the destination file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="allowInvalid">Whether to save even when validation reports errors.</param>
    public void Save(string path, bool allowInvalid = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var errors = SceneDefinitionSerializer.ValidateDocument(new() { Definitions = _definitions });
        if (!allowInvalid && errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        // Identity ambiguity is never allowed, even when saving an incomplete authoring draft.
        if (_definitions.Any(member => string.IsNullOrWhiteSpace(member.ID)) ||
            _definitions.Select(member => member.ID).Distinct(StringComparer.Ordinal).Count() != _definitions.Count)
            throw new InvalidDataException("Every definition needs a unique, non-empty logical identity.");
        path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var snapshots = _definitions.Select(member =>
        {
            var clone = SceneDefinitionSerializer.FromJson(SceneDefinitionSerializer.ToJson(member));
            clone.Source = PackedLocation is not null ? member.Source : SceneDefinitionSource.LooseDefinitionFile(FilePath ?? Path.Combine(BaseDirectory, "Untitled.gscn"));
            return clone;
        }).ToList();
        SceneDefinitionSerializer.SaveAll(path, snapshots);
        var saved = SceneDefinitionSerializer.LoadAll(path);
        for (int i = 0; i < _definitions.Count; i++)
        {
            var member = _definitions[i];
            var persisted = saved[i];
            for (int j = 0; j < member.TilesheetSources.Count; j++)
            {
                member.TilesheetSources[j].GtsPath = persisted.TilesheetSources[j].GtsPath;
                member.TilesheetSources[j].AssetsFilePath = persisted.TilesheetSources[j].AssetsFilePath;
            }
            for (int j = 0; j < member.AnimationSources.Count; j++)
            {
                member.AnimationSources[j].GaniPath = persisted.AnimationSources[j].GaniPath;
                member.AnimationSources[j].AssetsFilePath = persisted.AnimationSources[j].AssetsFilePath;
            }
            member.Source = SceneDefinitionSource.LooseDefinitionFile(path);
        }
        FilePath = path;
        PackedLocation = null;
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

    private List<SceneDefinition> _definitions;

    /// <summary>Gets all logical definitions owned by this physical document.</summary>
    public IReadOnlyList<SceneDefinition> Definitions => _definitions.AsReadOnly();

    /// <summary>Gets the current logical definition without reconstructing its metadata.</summary>
    public SceneDefinition SelectedDefinition => Definition;

    /// <summary>Selects an existing member without changing the physical document's dirty state.</summary>
    /// <param name="definition">The member to select.</param>
    public void SelectDefinition(SceneDefinition definition)
    {
        if (!_definitions.Contains(definition)) throw new ArgumentException("Definition does not belong to this document.", nameof(definition));
        Definition = definition;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds and selects a new logical definition with a unique default identity.</summary>
    /// <returns>The new member.</returns>
    public SceneDefinition AddDefinition()
    {
        var definition = Create(BaseDirectory).Definition;
        definition.ID = UniqueDefinitionIdentity("scene");
        _definitions.Add(definition);
        Definition = definition;
        MarkChanged();
        return definition;
    }

    /// <summary>Duplicates and selects the current definition with a unique identity.</summary>
    /// <returns>The new independent member.</returns>
    public SceneDefinition DuplicateDefinition()
    {
        var definition = SceneDefinitionSerializer.FromJson(SceneDefinitionSerializer.ToJson(Definition));
        definition.Source = Definition.Source;
        definition.ID = UniqueDefinitionIdentity(Definition.ID + "-copy");
        _definitions.Add(definition);
        Definition = definition;
        MarkChanged();
        return definition;
    }

    /// <summary>Removes a member while retaining at least one editable definition.</summary>
    /// <param name="definition">The member to remove.</param>
    /// <returns>Whether the member was removed.</returns>
    public bool RemoveDefinition(SceneDefinition definition)
    {
        if (_definitions.Count == 1 || !_definitions.Remove(definition)) return false;
        if (ReferenceEquals(Definition, definition)) Definition = _definitions[0];
        MarkChanged();
        return true;
    }

    /// <summary>Renames the selected member, rejecting empty or duplicate identities.</summary>
    /// <param name="identity">The new case-sensitive logical identity.</param>
    public void RenameDefinition(string identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        if (_definitions.Any(member => !ReferenceEquals(member, Definition) && member.ID == identity))
            throw new InvalidDataException($"Duplicate definition identity '{identity}'.");
        Definition.ID = identity;
        MarkChanged();
    }

    /// <summary>Imports selected members without replacing existing definitions or copying resources.</summary>
    /// <param name="path">A same-format source file; legacy singletons are supported.</param>
    /// <param name="select">Selects source indices. Omit to import all; return null or an empty list to cancel.</param>
    /// <returns>The number imported. The first imported member becomes selected.</returns>
    public int ImportDefinitions(string path, Func<IReadOnlyList<SceneDefinition>, IReadOnlyList<int>?>? select = null)
    {
        path = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(path), ".gscn", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a .gscn definition document.", nameof(path));
        var sourceDirectory = Path.GetDirectoryName(path)!;
        string? Rebase(string? value) => DefinitionImport.Rebase(value, sourceDirectory, BaseDirectory);
        var imported = DefinitionImport.Prepare(path, Definitions, SceneDefinitionSerializer.LoadAll, select, member =>
        {
            var clone = SceneDefinitionSerializer.FromJson(SceneDefinitionSerializer.ToJson(member));
            foreach (var source in clone.TilesheetSources ?? [])
            {
                source.GtsPath = Rebase(source.GtsPath);
                source.AssetsFilePath = Rebase(source.AssetsFilePath);
            }
            foreach (var source in clone.AnimationSources ?? [])
            {
                source.GaniPath = Rebase(source.GaniPath);
                source.AssetsFilePath = Rebase(source.AssetsFilePath);
            }
            return clone;
        }, member => member.ID, (member, identity) => member.ID = identity, "scene");
        if (imported.Count == 0) return 0;
        _definitions.AddRange(imported);
        Definition = imported[0];
        MarkChanged();
        return imported.Count;
    }

    private string UniqueDefinitionIdentity(string basis)
    {
        var candidate = basis;
        for (int i = 2; _definitions.Any(member => member.ID == candidate); i++) candidate = basis + "-" + i;
        return candidate;
    }
}
