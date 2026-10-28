using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Scenes.Editing;

/// <summary>UI-independent editing session for all logical definitions in one GSCN document.</summary>
public sealed partial class SceneDocument
{
    /// <summary>Adds a validated packed tilesheet dependency; rejects conflicting logical identities.</summary>
    public void AddPackedTilesheetSource(string tilesheet, string archivePath, string entryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tilesheet);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);
        if (Definition.TilesheetSources.Any(source => string.Equals(source.Tilesheet, tilesheet, StringComparison.Ordinal)))
            throw new InvalidOperationException($"A tilesheet source named '{tilesheet}' already exists. Remove the existing source before adding another.");
        Definition.TilesheetSources.Add(SceneTilesheetSourceDefinition.Packed(tilesheet,
            Path.GetRelativePath(BaseDirectory, Path.GetFullPath(archivePath)), entryName));
        MarkChanged();
    }

    /// <summary>Adds a validated packed animation dependency; rejects conflicting logical identities.</summary>
    public void AddPackedAnimationSource(string key, string archivePath, string entryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);
        if (Definition.AnimationSources.Any(source => string.Equals(source.AnimationKey, key, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"An animation source named '{key}' already exists. Remove the existing source before adding another.");
        Definition.AnimationSources.Add(SceneAnimationSourceDefinition.Packed(key,
            Path.GetRelativePath(BaseDirectory, Path.GetFullPath(archivePath)), entryName));
        MarkChanged();
    }

    /// <summary>Gets the selected mutable logical definition; direct edits require <see cref="MarkChanged"/>.</summary>
    public SceneDefinition Definition { get; private set; }
    /// <summary>Gets the full loose-file path, or null for an unsaved or packed document.</summary>
    public string? FilePath { get; private set; }
    /// <summary>Gets the absolute directory used to resolve relative dependencies; for packed documents, the containing archive directory.</summary>
    public string BaseDirectory { get; private set; }
    /// <summary>
    /// Gets whether the document contains unsaved changes.
    /// </summary>
    public bool IsDirty { get; private set; }
    /// <summary>Occurs synchronously when content is marked modified, selection changes, or a save is accepted.</summary>
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

    /// <summary>Loads all logical definitions from a loose GSCN file and selects the first member.</summary>
    /// <param name="path">Source file path, resolved against the process current directory when relative.</param>
    /// <returns>A clean editing session retaining the file's definition order.</returns>
    /// <exception cref="InvalidDataException">The loaded document contains no definitions.</exception>
    public static SceneDocument Open(string path)
    {
        path = Path.GetFullPath(path);
        var definitions = SceneDefinitionSerializer.LoadAll(path).ToList();
        if (definitions.Count == 0) throw new InvalidDataException("Document contains no definitions.");
        var document = new SceneDocument(definitions[0], path, Path.GetDirectoryName(path)!, false);
        document._definitions = definitions;
        return document;
    }

    /// <summary>Creates an unsaved GSCN document with one default definition and marks it dirty.</summary>
    /// <param name="directory">Base directory for dependency resolution; no file or directory is created.</param>
    /// <returns>A new editing session with no loose or packed save location.</returns>
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

    /// <summary>Adds a uniquely named 10-by-10 layer with 32-pixel tiles to the selected scene and marks the document dirty.</summary>
    /// <returns>The new layer, with its initial Z order set to the preceding layer count.</returns>
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

    /// <summary>Removes a layer from the selected scene and marks the document dirty if found.</summary>
    /// <param name="layer">Layer instance to remove.</param>
    /// <returns>True if removed; false if the layer was absent.</returns>
    public bool RemoveLayer(SceneLayerDefinition layer)
    {
        if (!Definition.Layers.Remove(layer))
            return false;
        MarkChanged();
        return true;
    }

    /// <summary>Finds the first explicitly stored tile at the requested coordinates without changing the layer.</summary>
    /// <param name="layer">Layer whose explicit tile overrides are searched.</param>
    /// <param name="x">Zero-based tile column.</param>
    /// <param name="y">Zero-based tile row.</param>
    /// <returns>The stored tile override, or null when the coordinate uses implicit layer defaults.</returns>
    public SceneLayerTileDefinition? FindTile(
        SceneLayerDefinition layer,
        int x,
        int y) =>
        layer.Tiles.FirstOrDefault(tile =>
            tile.X == x &&
            tile.Y == y);

    /// <summary>Gets a stored tile override or adds one at valid layer coordinates and marks the document dirty.</summary>
    /// <param name="layer">Layer to search and, if necessary, modify.</param>
    /// <param name="x">Zero-based column within the layer.</param>
    /// <param name="y">Zero-based row within the layer.</param>
    /// <returns>The existing or newly added mutable tile override.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Either coordinate lies outside the layer grid.</exception>
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

    /// <summary>Removes all explicit tile overrides at the coordinates, restoring implicit layer defaults there.</summary>
    /// <param name="layer">Layer whose tile overrides are modified.</param>
    /// <param name="x">Zero-based tile column.</param>
    /// <param name="y">Zero-based tile row.</param>
    /// <returns>True if any overrides were removed and the document was marked dirty; otherwise false.</returns>
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

    /// <summary>Replaces the selected definition's matching tilesheet sources with a loose reference and marks it dirty if changed.</summary>
    /// <param name="tilesheet">Nonempty, case-sensitive logical tilesheet name.</param>
    /// <param name="gtsPath">Loose GTS path, normalized against the process current directory and stored relative to the document when possible.</param>
    /// <returns>False if an existing loose source already resolves to that path; otherwise true.</returns>
    /// <exception cref="ArgumentException">A name or path is null, empty, or whitespace.</exception>
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

    /// <summary>Replaces matching animation sources in the selected scene with a loose reference and marks it dirty if changed.</summary>
    /// <param name="animationKey">Nonempty logical animation key, matched without case.</param>
    /// <param name="ganiPath">Loose GANI path, normalized against the process current directory and stored relative to the document when possible.</param>
    /// <returns>False if an existing loose source already resolves to that path; otherwise true.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    /// <exception cref="ArgumentException">Either argument is empty or whitespace.</exception>
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

    /// <summary>Removes all source entries matching a tilesheet name from the selected definition.</summary>
    /// <param name="tilesheet">Case-sensitive logical tilesheet name.</param>
    /// <returns>True if any entries were removed and the document was marked dirty; otherwise false.</returns>
    public bool RemoveTilesheetSource(string tilesheet)
    {
        int removed = Definition.TilesheetSources.RemoveAll(source =>
            string.Equals(source.Tilesheet, tilesheet, StringComparison.Ordinal));
        if (removed == 0)
            return false;
        MarkChanged();
        return true;
    }

    /// <summary>Removes all animation sources matching the key from the selected scene.</summary>
    /// <param name="animationKey">Logical animation key matched without case.</param>
    /// <returns>True if entries were removed and the document was marked dirty; otherwise false.</returns>
    public bool RemoveAnimationSource(string animationKey)
    {
        int removed = Definition.AnimationSources.RemoveAll(source =>
            string.Equals(source.AnimationKey, animationKey, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
            return false;
        MarkChanged();
        return true;
    }

    /// <summary>Resolves a document-relative resource path to an absolute path without checking existence.</summary>
    /// <param name="path">A dependency path, interpreted relative to <see cref="BaseDirectory"/> unless rooted.</param>
    /// <returns>The normalized absolute resource path.</returns>
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

    /// <summary>Validates all logical definitions and their document-level identities without loading runtime resources.</summary>
    /// <returns>The validation errors; an empty collection indicates that validation passed.</returns>
    public IReadOnlyList<string> Validate() =>
        SceneDefinitionSerializer.ValidateDocument(new SceneDefinitionDocument { Definitions = _definitions });

    /// <summary>Saves every logical member to a loose file, rebases dependencies, and adopts that file as the editing location.</summary>
    /// <param name="path">Destination path; missing parent directories are created and existing files may be overwritten.</param>
    /// <param name="allowInvalid">Whether to bypass ordinary validation errors; logical identities must still be nonempty and unique.</param>
    /// <remarks>On success, clears the packed location and dirty flag and raises <see cref="Changed"/>. Saving a packed document leaves its original archive entry unchanged.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Validation fails when <paramref name="allowInvalid"/> is false.</exception>
    /// <exception cref="InvalidDataException">Logical identities are empty or duplicated.</exception>
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
            clone.Source = PackedLocation is { } packed ? SceneDefinitionSource.PackedDefinitionFile(packed.Path, packed.AssetEntryName!) : SceneDefinitionSource.LooseDefinitionFile(FilePath ?? Path.Combine(BaseDirectory, "Untitled.gscn"));
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

    /// <summary>Selects an existing member without changing the physical document's dirty state and raises <see cref="Changed"/>.</summary>
    /// <param name="definition">The member instance already owned by this document.</param>
    /// <exception cref="ArgumentException"><paramref name="definition"/> does not belong to this document.</exception>
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

    /// <summary>Removes a member, marks the document dirty, and selects the first remaining member if necessary.</summary>
    /// <param name="definition">The member instance to remove.</param>
    /// <returns>True if removed; false if it is absent or is the document's only member.</returns>
    public bool RemoveDefinition(SceneDefinition definition)
    {
        if (_definitions.Count == 1 || !_definitions.Remove(definition)) return false;
        if (ReferenceEquals(Definition, definition)) Definition = _definitions[0];
        MarkChanged();
        return true;
    }

    /// <summary>Renames the selected member and marks the document dirty, without rewriting references in other documents.</summary>
    /// <param name="identity">The new case-sensitive logical identity.</param>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="identity"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidDataException">Another member already uses the identity.</exception>
    public void RenameDefinition(string identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        if (_definitions.Any(member => !ReferenceEquals(member, Definition) && member.ID == identity))
            throw new InvalidDataException($"Duplicate definition identity '{identity}'.");
        Definition.ID = identity;
        MarkChanged();
    }

    /// <summary>Imports selected GSCN members as independent copies, rebasing dependencies without copying resources.</summary>
    /// <param name="path">A same-format source file; legacy singletons are supported.</param>
    /// <param name="select">Selects zero-based source indices. Omit to import all; return null or an empty list to cancel.</param>
    /// <returns>The number imported in source order; zero leaves the document unchanged.</returns>
    /// <remarks>Conflicting logical identities receive unique suffixes. The first imported member becomes selected and the document is marked dirty.</remarks>
    /// <exception cref="ArgumentException">The file extension is not .gscn, or selection contains duplicate or out-of-range indices.</exception>
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
