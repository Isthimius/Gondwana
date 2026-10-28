using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Sprites.Editing;

/// <summary>UI-independent authoring session. Never creates runtime sprites or scenes.</summary>
public sealed partial class SpriteDocument
{
    /// <summary>Adds a validated packed tilesheet dependency; rejects conflicting logical identities.</summary>
    public void AddPackedTilesheetSource(string tilesheet, string archivePath, string entryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tilesheet);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);
        if (Definition.TilesheetSources.Any(source => string.Equals(source.Tilesheet, tilesheet, StringComparison.Ordinal)))
            throw new InvalidOperationException($"A tilesheet source named '{tilesheet}' already exists. Remove the existing source before adding another.");
        Definition.TilesheetSources.Add(SpriteTilesheetSourceDefinition.Packed(tilesheet,
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
        Definition.AnimationSources.Add(SpriteAnimationSourceDefinition.Packed(key,
            Path.GetRelativePath(BaseDirectory, Path.GetFullPath(archivePath)), entryName));
        MarkChanged();
    }

    /// <summary>Adds a validated packed scene dependency; rejects conflicting logical identities.</summary>
    public void AddPackedSceneSource(string id, string archivePath, string entryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);
        if (Definition.SceneSources.Any(source => string.Equals(source.SceneId, id, StringComparison.Ordinal)))
            throw new InvalidOperationException($"A scene source named '{id}' already exists. Remove the existing source before adding another.");
        Definition.SceneSources.Add(SpriteSceneSourceDefinition.Packed(id,
            Path.GetRelativePath(BaseDirectory, Path.GetFullPath(archivePath)), entryName));
        MarkChanged();
    }

    /// <summary>Gets the selected mutable logical definition; direct edits require <see cref="MarkChanged"/>.</summary>
    public SpriteDefinition Definition { get; private set; }
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

    private SpriteDocument(SpriteDefinition definition, string directory, string? path, bool dirty)
    {
        _definitions = [definition];
        Definition = definition;
        BaseDirectory = Path.GetFullPath(directory);
        if (string.IsNullOrWhiteSpace(definition.Key)) definition.Key = "sprite";
        FilePath = path;
        IsDirty = dirty;
    }

    /// <summary>Creates an unsaved GSPR document with one default definition and marks it dirty.</summary>
    /// <param name="directory">Base directory for dependency resolution; no file or directory is created.</param>
    /// <returns>A new editing session with no loose or packed save location.</returns>
    public static SpriteDocument Create(string directory) => new(new() { Source = SpriteDefinitionSource.Generated() }, directory, null, true);
    /// <summary>Loads all logical definitions from a loose GSPR file and selects the first member.</summary>
    /// <param name="path">Source file path, resolved against the process current directory when relative.</param>
    /// <returns>A clean editing session retaining the file's definition order.</returns>
    /// <exception cref="InvalidDataException">The loaded document contains no definitions.</exception>
    public static SpriteDocument Open(string path)
    {
        path = Path.GetFullPath(path);
        var definitions = SpriteDefinitionSerializer.LoadAll(path).ToList();
        if (definitions.Count == 0) throw new InvalidDataException("Document contains no definitions.");
        var document = new SpriteDocument(definitions[0], Path.GetDirectoryName(path)!, path, false);
        document._definitions = definitions;
        return document;
    }

    /// <summary>Adds a sprite instance with a new GUID and unique nickname to the selected definition and marks it dirty.</summary>
    /// <returns>The new mutable sprite instance definition.</returns>
    public SpriteInstanceDefinition AddSprite()
    {
        var sprite = new SpriteInstanceDefinition { Id = Guid.NewGuid(), Nickname = UniqueName("sprite") };
        Definition.Sprites.Add(sprite);
        MarkChanged();
        return sprite;
    }

    /// <summary>Deep-copies a sprite instance into the selected definition with a new GUID and unique nickname.</summary>
    /// <param name="source">Sprite instance to copy through native serialization.</param>
    /// <returns>The added mutable instance; the document is marked dirty.</returns>
    public SpriteInstanceDefinition DuplicateSprite(SpriteInstanceDefinition source)
    {
        var sprite = SpriteDefinitionSerializer.FromJson(SpriteDefinitionSerializer.ToJson(new() { Sprites = [source] })).Sprites[0];
        sprite.Id = Guid.NewGuid();
        sprite.Nickname = UniqueName(source.Nickname ?? "sprite");
        Definition.Sprites.Add(sprite);
        MarkChanged();
        return sprite;
    }

    /// <summary>Removes a sprite instance from the selected definition and marks the document dirty when found.</summary>
    /// <param name="sprite">The instance definition to remove.</param>
    /// <returns>True if removed; false if absent.</returns>
    public bool RemoveSprite(SpriteInstanceDefinition sprite)
    {
        if (!Definition.Sprites.Remove(sprite)) return false;
        MarkChanged();
        return true;
    }

    private string UniqueName(string basis)
    {
        var name = basis;
        for (int i = 2; Definition.Sprites.Any(sprite => sprite.Nickname == name); i++) name = basis + "-" + i;
        return name;
    }

    /// <summary>Replaces matching tilesheet source entries in the selected definition and marks the document dirty.</summary>
    /// <param name="name">Logical tilesheet name, matched using case-sensitive comparison.</param>
    /// <param name="path">Loose GTS file path; normalized against the process current directory and stored relative to the document.</param>
    /// <remarks>Does not load the dependency or verify that its logical member exists.</remarks>
    public void SetLooseTilesheetSource(string name, string path)
    {
        Definition.TilesheetSources.RemoveAll(source => source.Tilesheet == name);
        Definition.TilesheetSources.Add(SpriteTilesheetSourceDefinition.Loose(name, Path.GetRelativePath(BaseDirectory, Path.GetFullPath(path))));
        MarkChanged();
    }

    /// <summary>Replaces matching scene source entries in the selected definition and marks the document dirty.</summary>
    /// <param name="id">Logical scene ID, matched using case-sensitive comparison.</param>
    /// <param name="path">Loose GSCN file path; normalized against the process current directory and stored relative to the document.</param>
    /// <remarks>Does not load the dependency or verify that its logical member exists.</remarks>
    public void SetLooseSceneSource(string id, string path)
    {
        Definition.SceneSources.RemoveAll(source => source.SceneId == id);
        Definition.SceneSources.Add(SpriteSceneSourceDefinition.Loose(id, Path.GetRelativePath(BaseDirectory, Path.GetFullPath(path))));
        MarkChanged();
    }

    /// <summary>Replaces matching animation source entries in the selected definition and marks the document dirty.</summary>
    /// <param name="key">Logical animation key, matched using case-insensitive comparison.</param>
    /// <param name="path">Loose GANI file path; normalized against the process current directory and stored relative to the document.</param>
    /// <remarks>Does not load the dependency or verify that its logical member exists.</remarks>
    public void SetLooseAnimationSource(string key, string path)
    {
        Definition.AnimationSources.RemoveAll(source => string.Equals(source.AnimationKey, key, StringComparison.OrdinalIgnoreCase));
        Definition.AnimationSources.Add(SpriteAnimationSourceDefinition.Loose(
            key, Path.GetRelativePath(BaseDirectory, Path.GetFullPath(path))));
        MarkChanged();
    }

    /// <summary>Resolves a document-relative resource path to an absolute path without checking existence.</summary>
    /// <param name="path">A dependency path, interpreted relative to <see cref="BaseDirectory"/> unless rooted.</param>
    /// <returns>The normalized absolute resource path.</returns>
    public string ResolveReferencePath(string path) => Path.GetFullPath(path, BaseDirectory);
    /// <summary>Validates all logical definitions and their document-level identities without loading runtime resources.</summary>
    /// <returns>The validation errors; an empty collection indicates that validation passed.</returns>
    public IReadOnlyList<string> Validate() => SpriteDefinitionSerializer.ValidateDocument(new SpriteDefinitionDocument { Definitions = _definitions });
    /// <summary>
    /// Marks the document as modified and notifies listeners.
    /// </summary>
    public void MarkChanged() { IsDirty = true; Changed?.Invoke(this, EventArgs.Empty); }

    /// <summary>Saves every logical member to a loose file, rebases dependencies, and adopts that file as the editing location.</summary>
    /// <param name="path">Destination path; missing parent directories are created and existing files may be overwritten.</param>
    /// <param name="allowInvalid">Whether to bypass ordinary validation errors; logical identities must still be nonempty and unique.</param>
    /// <remarks>On success, clears the packed location and dirty flag and raises <see cref="Changed"/>. Saving a packed document leaves its original archive entry unchanged.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidDataException">Validation fails when <paramref name="allowInvalid"/> is false.</exception>
    /// <exception cref="InvalidDataException">Logical identities are empty or duplicated.</exception>
    public void Save(string path, bool allowInvalid = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var errors = SpriteDefinitionSerializer.ValidateDocument(new() { Definitions = _definitions });
        if (!allowInvalid && errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        // Identity ambiguity is never allowed, even when saving an incomplete authoring draft.
        if (_definitions.Any(member => string.IsNullOrWhiteSpace(member.Key)) ||
            _definitions.Select(member => member.Key).Distinct(StringComparer.Ordinal).Count() != _definitions.Count)
            throw new InvalidDataException("Every definition needs a unique, non-empty logical identity.");
        path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var snapshots = _definitions.Select(member =>
        {
            var clone = SpriteDefinitionSerializer.FromJson(SpriteDefinitionSerializer.ToJson(member));
            clone.Source = PackedLocation is { } packed ? SpriteDefinitionSource.PackedDefinitionFile(packed.Path, packed.AssetEntryName!) : SpriteDefinitionSource.LooseDefinitionFile(FilePath ?? Path.Combine(BaseDirectory, "Untitled.gspr"));
            return clone;
        }).ToList();
        SpriteDefinitionSerializer.SaveAll(path, snapshots);
        var saved = SpriteDefinitionSerializer.LoadAll(path);
        for (int i = 0; i < _definitions.Count; i++)
        {
            var member = _definitions[i];
            var persisted = saved[i];
            for (int j = 0; j < member.TilesheetSources.Count; j++)
            {
                member.TilesheetSources[j].GtsPath = persisted.TilesheetSources[j].GtsPath;
                member.TilesheetSources[j].AssetsFilePath = persisted.TilesheetSources[j].AssetsFilePath;
            }
            for (int j = 0; j < member.SceneSources.Count; j++)
            {
                member.SceneSources[j].GscnPath = persisted.SceneSources[j].GscnPath;
                member.SceneSources[j].AssetsFilePath = persisted.SceneSources[j].AssetsFilePath;
            }
            for (int j = 0; j < member.AnimationSources.Count; j++)
            {
                member.AnimationSources[j].GaniPath = persisted.AnimationSources[j].GaniPath;
                member.AnimationSources[j].AssetsFilePath = persisted.AnimationSources[j].AssetsFilePath;
            }
            member.Source = SpriteDefinitionSource.LooseDefinitionFile(path);
        }
        FilePath = path;
        PackedLocation = null;
        BaseDirectory = directory;
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private List<SpriteDefinition> _definitions;

    /// <summary>Gets all logical definitions owned by this physical document.</summary>
    public IReadOnlyList<SpriteDefinition> Definitions => _definitions.AsReadOnly();

    /// <summary>Gets the current logical definition without reconstructing its metadata.</summary>
    public SpriteDefinition SelectedDefinition => Definition;

    /// <summary>Selects an existing member without changing the physical document's dirty state and raises <see cref="Changed"/>.</summary>
    /// <param name="definition">The member instance already owned by this document.</param>
    /// <exception cref="ArgumentException"><paramref name="definition"/> does not belong to this document.</exception>
    public void SelectDefinition(SpriteDefinition definition)
    {
        if (!_definitions.Contains(definition)) throw new ArgumentException("Definition does not belong to this document.", nameof(definition));
        Definition = definition;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds and selects a new logical definition with a unique default identity.</summary>
    /// <returns>The new member.</returns>
    public SpriteDefinition AddDefinition()
    {
        var definition = Create(BaseDirectory).Definition;
        definition.Key = UniqueDefinitionIdentity("sprite");
        _definitions.Add(definition);
        Definition = definition;
        MarkChanged();
        return definition;
    }

    /// <summary>Duplicates and selects the current definition with a unique identity and marks the document dirty.</summary>
    /// <returns>The independent copy, with new sprite GUIDs and nonempty nicknames suffixed by the new definition key.</returns>
    public SpriteDefinition DuplicateDefinition()
    {
        var definition = SpriteDefinitionSerializer.FromJson(SpriteDefinitionSerializer.ToJson(Definition));
        definition.Source = Definition.Source;
        definition.Key = UniqueDefinitionIdentity(Definition.Key + "-copy");
        foreach (var sprite in definition.Sprites) { sprite.Id = Guid.NewGuid(); if (!string.IsNullOrWhiteSpace(sprite.Nickname)) sprite.Nickname += "-" + definition.Key; }
        _definitions.Add(definition);
        Definition = definition;
        MarkChanged();
        return definition;
    }

    /// <summary>Removes a member, marks the document dirty, and selects the first remaining member if necessary.</summary>
    /// <param name="definition">The member instance to remove.</param>
    /// <returns>True if removed; false if it is absent or is the document's only member.</returns>
    public bool RemoveDefinition(SpriteDefinition definition)
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
        if (_definitions.Any(member => !ReferenceEquals(member, Definition) && member.Key == identity))
            throw new InvalidDataException($"Duplicate definition identity '{identity}'.");
        Definition.Key = identity;
        MarkChanged();
    }

    /// <summary>Imports selected GSPR members as independent copies, rebasing dependencies without copying resources.</summary>
    /// <param name="path">A same-format source file; legacy singletons are supported.</param>
    /// <param name="select">Selects zero-based source indices. Omit to import all; return null or an empty list to cancel.</param>
    /// <returns>The number imported in source order; zero leaves the document unchanged.</returns>
    /// <remarks>Conflicting logical identities receive unique suffixes. The first imported member becomes selected and the document is marked dirty.</remarks>
    /// <exception cref="ArgumentException">The file extension is not .gspr, or selection contains duplicate or out-of-range indices.</exception>
    public int ImportDefinitions(string path, Func<IReadOnlyList<SpriteDefinition>, IReadOnlyList<int>?>? select = null)
    {
        path = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(path), ".gspr", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a .gspr definition document.", nameof(path));
        var sourceDirectory = Path.GetDirectoryName(path)!;
        string? Rebase(string? value) => DefinitionImport.Rebase(value, sourceDirectory, BaseDirectory);
        var imported = DefinitionImport.Prepare(path, Definitions, SpriteDefinitionSerializer.LoadAll, select, member =>
        {
            var clone = SpriteDefinitionSerializer.FromJson(SpriteDefinitionSerializer.ToJson(member));
            foreach (var source in clone.TilesheetSources ?? [])
            {
                source.GtsPath = Rebase(source.GtsPath);
                source.AssetsFilePath = Rebase(source.AssetsFilePath);
            }
            foreach (var source in clone.SceneSources ?? [])
            {
                source.GscnPath = Rebase(source.GscnPath);
                source.AssetsFilePath = Rebase(source.AssetsFilePath);
            }
            foreach (var source in clone.AnimationSources ?? [])
            {
                source.GaniPath = Rebase(source.GaniPath);
                source.AssetsFilePath = Rebase(source.AssetsFilePath);
            }
            return clone;
        }, member => member.Key, (member, identity) => member.Key = identity, "sprites");
        if (imported.Count == 0) return 0;
        _definitions.AddRange(imported);
        Definition = imported[0];
        MarkChanged();
        return imported.Count;
    }

    private string UniqueDefinitionIdentity(string basis)
    {
        var candidate = basis;
        for (int i = 2; _definitions.Any(member => member.Key == candidate); i++) candidate = basis + "-" + i;
        return candidate;
    }
}
