using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Animations.Editing;

/// <summary>
/// UI-independent editing session for one GANI animation definition.
/// </summary>
public sealed class AnimationDocument
{
    /// <summary>
    /// Gets the definition.
    /// </summary>
    public AnimationDefinition Definition { get; private set; }
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

    private AnimationDocument(
        AnimationDefinition definition,
        string? filePath,
        string baseDirectory,
        bool isDirty)
    {
        _definitions = [definition];
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        FilePath = filePath;
        BaseDirectory = Path.GetFullPath(baseDirectory);
        IsDirty = isDirty;
    }

    /// <summary>
    /// Opens the supplied file for use by the animation document.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The resulting animation document.</returns>
    public static AnimationDocument Open(string path)
    {
        path = Path.GetFullPath(path);
        var definitions = AnimationDefinitionSerializer.LoadAll(path).ToList();
        if (definitions.Count == 0) throw new InvalidDataException("Document contains no definitions.");
        var document = new AnimationDocument(definitions[0], path, Path.GetDirectoryName(path)!, false);
        document._definitions = definitions;
        return document;
    }

    /// <summary>
    /// Creates a new animation document.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The resulting animation document.</returns>
    public static AnimationDocument Create(string directory) =>
        new(
            new AnimationDefinition
            {
                Key = "Untitled",
                ThrottleTime = 0.1,
                CycleType = CycleType.Repeating,
                Source = AnimationDefinitionSource.Generated()
            },
            filePath: null,
            directory,
            isDirty: true);

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
        AnimationDefinitionSerializer.ValidateDocument(new AnimationDefinitionDocument { Definitions = _definitions });

    /// <summary>
    /// Adds or updates a tilesheet reference to a loose GTS file.
    /// </summary>
    /// <param name="tilesheet">The tilesheet.</param>
    /// <param name="gtsPath">The path to the loose GTS tilesheet definition.</param>
    /// <returns><see langword="true"/> if the source reference changed; otherwise, <see langword="false"/>.</returns>
    public bool SetLooseTilesheetSource(
        string tilesheet,
        string gtsPath)
    {
        if (string.IsNullOrWhiteSpace(tilesheet))
            throw new ArgumentException("Tilesheet name must be a non-empty string.", nameof(tilesheet));

        if (string.IsNullOrWhiteSpace(gtsPath))
            throw new ArgumentException("GTS path must be a non-empty string.", nameof(gtsPath));

        string fullPath = Path.GetFullPath(gtsPath);
        string persistedPath = MakeReferencePath(fullPath, BaseDirectory);

        var existing = Definition.TilesheetSources.FirstOrDefault(source =>
            string.Equals(
                source.Tilesheet,
                tilesheet,
                StringComparison.Ordinal));

        if (existing is not null &&
            existing.Kind == AnimationTilesheetSourceKind.LooseDefinitionFile &&
            !string.IsNullOrWhiteSpace(existing.GtsPath))
        {
            string existingFullPath = ResolveReferencePath(existing.GtsPath);
            if (string.Equals(
                    existingFullPath,
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        Definition.TilesheetSources.RemoveAll(source =>
            string.Equals(
                source.Tilesheet,
                tilesheet,
                StringComparison.Ordinal));

        Definition.TilesheetSources.Add(
            AnimationTilesheetSourceDefinition.Loose(
                tilesheet,
                persistedPath));

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
            string.Equals(
                source.Tilesheet,
                tilesheet,
                StringComparison.Ordinal));

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
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Reference path must be a non-empty string.", nameof(path));

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(path, BaseDirectory);
    }

    /// <summary>
    /// Saves the current content to the destination file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="allowInvalid">Whether to save even when validation reports errors.</param>
    public void Save(string path, bool allowInvalid = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var errors = AnimationDefinitionSerializer.ValidateDocument(new() { Definitions = _definitions });
        if (!allowInvalid && errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        // Identity ambiguity is never allowed, even when saving an incomplete authoring draft.
        if (_definitions.Any(member => string.IsNullOrWhiteSpace(member.Key)) ||
            _definitions.Select(member => member.Key).Distinct(StringComparer.Ordinal).Count() != _definitions.Count)
            throw new InvalidDataException("Every definition needs a unique, non-empty logical identity.");
        path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var snapshots = _definitions.Select(member =>
        {
            var clone = AnimationDefinitionSerializer.FromJson(AnimationDefinitionSerializer.ToJson(member));
            clone.Source = AnimationDefinitionSource.LooseDefinitionFile(FilePath ?? Path.Combine(BaseDirectory, "Untitled.gani"));
            return clone;
        }).ToList();
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            AnimationDefinitionSerializer.SaveAll(temporary, snapshots);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        var saved = AnimationDefinitionSerializer.LoadAll(path);
        for (int i = 0; i < _definitions.Count; i++)
        {
            var member = _definitions[i];
            var persisted = saved[i];
            for (int j = 0; j < member.TilesheetSources.Count; j++)
            {
                member.TilesheetSources[j].GtsPath = persisted.TilesheetSources[j].GtsPath;
                member.TilesheetSources[j].AssetsFilePath = persisted.TilesheetSources[j].AssetsFilePath;
            }
            member.Source = AnimationDefinitionSource.LooseDefinitionFile(path);
        }
        FilePath = path;
        BaseDirectory = directory;
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string MakeReferencePath(
        string path,
        string baseDirectory)
    {
        string fullPath = Path.GetFullPath(path);
        string fullBaseDirectory = Path.GetFullPath(baseDirectory);

        string? pathRoot = Path.GetPathRoot(fullPath);
        string? baseRoot = Path.GetPathRoot(fullBaseDirectory);

        if (!string.Equals(
                pathRoot,
                baseRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        return Path.GetRelativePath(
            fullBaseDirectory,
            fullPath);
    }

    private List<AnimationDefinition> _definitions;

    /// <summary>Gets all logical definitions owned by this physical document.</summary>
    public IReadOnlyList<AnimationDefinition> Definitions => _definitions.AsReadOnly();

    /// <summary>Gets the current logical definition without reconstructing its metadata.</summary>
    public AnimationDefinition SelectedDefinition => Definition;

    /// <summary>Selects an existing member without changing the physical document's dirty state.</summary>
    /// <param name="definition">The member to select.</param>
    public void SelectDefinition(AnimationDefinition definition)
    {
        if (!_definitions.Contains(definition)) throw new ArgumentException("Definition does not belong to this document.", nameof(definition));
        Definition = definition;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds and selects a new logical definition with a unique default identity.</summary>
    /// <returns>The new member.</returns>
    public AnimationDefinition AddDefinition()
    {
        var definition = Create(BaseDirectory).Definition;
        definition.Key = UniqueDefinitionIdentity("animation");
        _definitions.Add(definition);
        Definition = definition;
        MarkChanged();
        return definition;
    }

    /// <summary>Duplicates and selects the current definition with a unique identity.</summary>
    /// <returns>The new independent member.</returns>
    public AnimationDefinition DuplicateDefinition()
    {
        var definition = AnimationDefinitionSerializer.FromJson(AnimationDefinitionSerializer.ToJson(Definition));
        definition.Source = Definition.Source;
        definition.Key = UniqueDefinitionIdentity(Definition.Key + "-copy");
        _definitions.Add(definition);
        Definition = definition;
        MarkChanged();
        return definition;
    }

    /// <summary>Removes a member while retaining at least one editable definition.</summary>
    /// <param name="definition">The member to remove.</param>
    /// <returns>Whether the member was removed.</returns>
    public bool RemoveDefinition(AnimationDefinition definition)
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
    public int ImportDefinitions(string path, Func<IReadOnlyList<AnimationDefinition>, IReadOnlyList<int>?>? select = null)
    {
        path = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(path), ".gani", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a .gani definition document.", nameof(path));
        var sourceDirectory = Path.GetDirectoryName(path)!;
        string? Rebase(string? value) => DefinitionImport.Rebase(value, sourceDirectory, BaseDirectory);
        var imported = DefinitionImport.Prepare(path, Definitions, AnimationDefinitionSerializer.LoadAll, select, member =>
        {
            var clone = AnimationDefinitionSerializer.FromJson(AnimationDefinitionSerializer.ToJson(member));
            foreach (var source in clone.TilesheetSources ?? [])
            {
                source.GtsPath = Rebase(source.GtsPath);
                source.AssetsFilePath = Rebase(source.AssetsFilePath);
            }
            return clone;
        }, member => member.Key, (member, identity) => member.Key = identity, "animation");
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
