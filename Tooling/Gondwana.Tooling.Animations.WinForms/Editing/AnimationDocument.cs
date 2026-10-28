using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Animations.Editing;

/// <summary>UI-independent editing session for all logical definitions in one GANI document.</summary>
public sealed partial class AnimationDocument
{
    /// <summary>Adds a validated packed tilesheet dependency; rejects conflicting logical identities.</summary>
    public void AddPackedTilesheetSource(string tilesheet, string archivePath, string entryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tilesheet);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);
        if (Definition.TilesheetSources.Any(source => string.Equals(source.Tilesheet, tilesheet, StringComparison.Ordinal)))
            throw new InvalidOperationException($"A tilesheet source named '{tilesheet}' already exists. Remove the existing source before adding another.");
        Definition.TilesheetSources.Add(AnimationTilesheetSourceDefinition.Packed(tilesheet,
            Path.GetRelativePath(BaseDirectory, Path.GetFullPath(archivePath)), entryName));
        MarkChanged();
    }

    /// <summary>Gets the selected mutable logical definition; direct edits require <see cref="MarkChanged"/>.</summary>
    public AnimationDefinition Definition { get; private set; }
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

    /// <summary>Loads all logical definitions from a loose GANI file and selects the first member.</summary>
    /// <param name="path">Source file path, resolved against the process current directory when relative.</param>
    /// <returns>A clean editing session retaining the file's definition order.</returns>
    /// <exception cref="InvalidDataException">The loaded document contains no definitions.</exception>
    public static AnimationDocument Open(string path)
    {
        path = Path.GetFullPath(path);
        var definitions = AnimationDefinitionSerializer.LoadAll(path).ToList();
        if (definitions.Count == 0) throw new InvalidDataException("Document contains no definitions.");
        var document = new AnimationDocument(definitions[0], path, Path.GetDirectoryName(path)!, false);
        document._definitions = definitions;
        return document;
    }

    /// <summary>Creates an unsaved GANI document with one default definition and marks it dirty.</summary>
    /// <param name="directory">Base directory for dependency resolution; no file or directory is created.</param>
    /// <returns>A new editing session with no loose or packed save location.</returns>
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

    /// <summary>Validates all logical definitions and their document-level identities without loading runtime resources.</summary>
    /// <returns>The validation errors; an empty collection indicates that validation passed.</returns>
    public IReadOnlyList<string> Validate() =>
        AnimationDefinitionSerializer.ValidateDocument(new AnimationDefinitionDocument { Definitions = _definitions });

    /// <summary>Replaces the selected definition's matching tilesheet sources with a loose reference and marks it dirty if changed.</summary>
    /// <param name="tilesheet">Nonempty, case-sensitive logical tilesheet name.</param>
    /// <param name="gtsPath">Loose GTS path, normalized against the process current directory and stored relative to the document when possible.</param>
    /// <returns>False if an existing loose source already resolves to that path; otherwise true.</returns>
    /// <exception cref="ArgumentException">A name or path is null, empty, or whitespace.</exception>
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

    /// <summary>Removes all source entries matching a tilesheet name from the selected definition.</summary>
    /// <param name="tilesheet">Case-sensitive logical tilesheet name.</param>
    /// <returns>True if any entries were removed and the document was marked dirty; otherwise false.</returns>
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

    /// <summary>Resolves a document-relative resource path to an absolute path without checking existence.</summary>
    /// <param name="path">A dependency path, interpreted relative to <see cref="BaseDirectory"/> unless rooted.</param>
    /// <returns>The normalized absolute resource path.</returns>
    public string ResolveReferencePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Reference path must be a non-empty string.", nameof(path));

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(path, BaseDirectory);
    }

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
            clone.Source = PackedLocation is { } packed ? AnimationDefinitionSource.PackedDefinitionFile(packed.Path, packed.AssetEntryName!) : AnimationDefinitionSource.LooseDefinitionFile(FilePath ?? Path.Combine(BaseDirectory, "Untitled.gani"));
            return clone;
        }).ToList();
        AnimationDefinitionSerializer.SaveAll(path, snapshots);
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
        PackedLocation = null;
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

    /// <summary>Selects an existing member without changing the physical document's dirty state and raises <see cref="Changed"/>.</summary>
    /// <param name="definition">The member instance already owned by this document.</param>
    /// <exception cref="ArgumentException"><paramref name="definition"/> does not belong to this document.</exception>
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

    /// <summary>Removes a member, marks the document dirty, and selects the first remaining member if necessary.</summary>
    /// <param name="definition">The member instance to remove.</param>
    /// <returns>True if removed; false if it is absent or is the document's only member.</returns>
    public bool RemoveDefinition(AnimationDefinition definition)
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

    /// <summary>Imports selected GANI members as independent copies, rebasing dependencies without copying resources.</summary>
    /// <param name="path">A same-format source file; legacy singletons are supported.</param>
    /// <param name="select">Selects zero-based source indices. Omit to import all; return null or an empty list to cancel.</param>
    /// <returns>The number imported in source order; zero leaves the document unchanged.</returns>
    /// <remarks>Conflicting logical identities receive unique suffixes. The first imported member becomes selected and the document is marked dirty.</remarks>
    /// <exception cref="ArgumentException">The file extension is not .gani, or selection contains duplicate or out-of-range indices.</exception>
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
