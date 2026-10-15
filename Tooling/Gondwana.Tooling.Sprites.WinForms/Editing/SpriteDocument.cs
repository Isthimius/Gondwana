using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Sprites.Editing;

/// <summary>UI-independent authoring session. Never creates runtime sprites or scenes.</summary>
public sealed class SpriteDocument
{
    /// <summary>
    /// Gets the definition.
    /// </summary>
    public SpriteDefinition Definition { get; private set; }
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

    private SpriteDocument(SpriteDefinition definition, string directory, string? path, bool dirty)
    {
        _definitions = [definition];
        Definition = definition;
        BaseDirectory = Path.GetFullPath(directory);
        if (string.IsNullOrWhiteSpace(definition.Key)) definition.Key = "sprite";
        FilePath = path;
        IsDirty = dirty;
    }

    /// <summary>
    /// Creates a new sprite document.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The resulting sprite document.</returns>
    public static SpriteDocument Create(string directory) => new(new() { Source = SpriteDefinitionSource.Generated() }, directory, null, true);
    /// <summary>
    /// Opens the supplied file for use by the sprite document.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The resulting sprite document.</returns>
    public static SpriteDocument Open(string path)
    {
        path = Path.GetFullPath(path);
        var definitions = SpriteDefinitionSerializer.LoadAll(path).ToList();
        if (definitions.Count == 0) throw new InvalidDataException("Document contains no definitions.");
        var document = new SpriteDocument(definitions[0], Path.GetDirectoryName(path)!, path, false);
        document._definitions = definitions;
        return document;
    }

    /// <summary>
    /// Adds a new sprite definition to the document.
    /// </summary>
    /// <returns>The new sprite definition added to the document.</returns>
    public SpriteInstanceDefinition AddSprite()
    {
        var sprite = new SpriteInstanceDefinition { Id = Guid.NewGuid(), Nickname = UniqueName("sprite") };
        Definition.Sprites.Add(sprite);
        MarkChanged();
        return sprite;
    }

    /// <summary>
    /// Copies the selected sprite definition into a new document entry.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The copied sprite definition added to the document.</returns>
    public SpriteInstanceDefinition DuplicateSprite(SpriteInstanceDefinition source)
    {
        var sprite = SpriteDefinitionSerializer.FromJson(SpriteDefinitionSerializer.ToJson(new() { Sprites = [source] })).Sprites[0];
        sprite.Id = Guid.NewGuid();
        sprite.Nickname = UniqueName(source.Nickname ?? "sprite");
        Definition.Sprites.Add(sprite);
        MarkChanged();
        return sprite;
    }

    /// <summary>
    /// Removes the selected sprite definition from the document.
    /// </summary>
    /// <param name="sprite">The sprite.</param>
    /// <returns><see langword="true"/> if the entry was found and removed; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Adds or updates a tilesheet reference to a loose GTS file.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="path">The path.</param>
    public void SetLooseTilesheetSource(string name, string path)
    {
        Definition.TilesheetSources.RemoveAll(source => source.Tilesheet == name);
        Definition.TilesheetSources.Add(SpriteTilesheetSourceDefinition.Loose(name, Path.GetRelativePath(BaseDirectory, Path.GetFullPath(path))));
        MarkChanged();
    }

    /// <summary>
    /// Adds or updates a scene reference to a loose GSCN file.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="path">The path.</param>
    public void SetLooseSceneSource(string id, string path)
    {
        Definition.SceneSources.RemoveAll(source => source.SceneId == id);
        Definition.SceneSources.Add(SpriteSceneSourceDefinition.Loose(id, Path.GetRelativePath(BaseDirectory, Path.GetFullPath(path))));
        MarkChanged();
    }

    /// <summary>Adds or updates the authoring source for a registered GANI animation.</summary>
    public void SetLooseAnimationSource(string key, string path)
    {
        Definition.AnimationSources.RemoveAll(source => string.Equals(source.AnimationKey, key, StringComparison.OrdinalIgnoreCase));
        Definition.AnimationSources.Add(SpriteAnimationSourceDefinition.Loose(
            key, Path.GetRelativePath(BaseDirectory, Path.GetFullPath(path))));
        MarkChanged();
    }

    /// <summary>
    /// Resolves a document-relative resource path to an absolute path.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The absolute path of the referenced resource.</returns>
    public string ResolveReferencePath(string path) => Path.GetFullPath(path, BaseDirectory);
    /// <summary>
    /// Checks the definition and collects validation errors.
    /// </summary>
    /// <returns>The validation errors; an empty collection indicates that validation passed.</returns>
    public IReadOnlyList<string> Validate() => SpriteDefinitionSerializer.ValidateDocument(new SpriteDefinitionDocument { Definitions = _definitions });
    /// <summary>
    /// Marks the document as modified and notifies listeners.
    /// </summary>
    public void MarkChanged() { IsDirty = true; Changed?.Invoke(this, EventArgs.Empty); }

    /// <summary>
    /// Saves the current content to the destination file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="allowInvalid">Whether to save even when validation reports errors.</param>
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
            clone.Source = SpriteDefinitionSource.LooseDefinitionFile(FilePath ?? Path.Combine(BaseDirectory, "Untitled.gspr"));
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
        BaseDirectory = directory;
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private List<SpriteDefinition> _definitions;

    /// <summary>Gets all logical definitions owned by this physical document.</summary>
    public IReadOnlyList<SpriteDefinition> Definitions => _definitions.AsReadOnly();

    /// <summary>Gets the current logical definition without reconstructing its metadata.</summary>
    public SpriteDefinition SelectedDefinition => Definition;

    /// <summary>Selects an existing member without changing the physical document's dirty state.</summary>
    /// <param name="definition">The member to select.</param>
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

    /// <summary>Duplicates and selects the current definition with a unique identity.</summary>
    /// <returns>The new independent member.</returns>
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

    /// <summary>Removes a member while retaining at least one editable definition.</summary>
    /// <param name="definition">The member to remove.</param>
    /// <returns>Whether the member was removed.</returns>
    public bool RemoveDefinition(SpriteDefinition definition)
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
        if (_definitions.Any(member => !ReferenceEquals(member, Definition) && member.Key == identity))
            throw new InvalidDataException($"Duplicate definition identity '{identity}'.");
        Definition.Key = identity;
        MarkChanged();
    }

    /// <summary>Imports selected members without replacing existing definitions or copying resources.</summary>
    /// <param name="path">A same-format source file; legacy singletons are supported.</param>
    /// <param name="select">Selects source indices. Omit to import all; return null or an empty list to cancel.</param>
    /// <returns>The number imported. The first imported member becomes selected.</returns>
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
