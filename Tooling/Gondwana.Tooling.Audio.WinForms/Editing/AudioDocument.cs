using Gondwana.Audio.GSND;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Audio.Editing;

/// <summary>UI-independent editing session for all logical definitions in one GSND document.</summary>
public sealed partial class AudioDocument
{
    /// <summary>Gets the selected mutable logical definition; direct edits require <see cref="MarkChanged"/>.</summary>
    public AudioDefinition Definition { get; private set; }
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

    private AudioDocument(
        AudioDefinition definition,
        string? filePath,
        string baseDirectory,
        bool isDirty)
    {
        _definitions = [definition];
        Definition = definition;
        if (string.IsNullOrWhiteSpace(definition.Key)) definition.Key = "audio";
        FilePath = filePath;
        BaseDirectory = Path.GetFullPath(baseDirectory);
        IsDirty = isDirty;
    }

    /// <summary>Loads all logical definitions from a loose GSND file and selects the first member.</summary>
    /// <param name="path">Source file path, resolved against the process current directory when relative.</param>
    /// <returns>A clean editing session retaining the file's definition order.</returns>
    /// <exception cref="InvalidDataException">The loaded document contains no definitions.</exception>
    public static AudioDocument Open(string path)
    {
        path = Path.GetFullPath(path);
        var definitions = AudioDefinitionSerializer.LoadAll(path).ToList();
        if (definitions.Count == 0) throw new InvalidDataException("Document contains no definitions.");
        var document = new AudioDocument(definitions[0], path, Path.GetDirectoryName(path)!, false);
        document._definitions = definitions;
        return document;
    }

    /// <summary>Creates an unsaved GSND document with one default definition and marks it dirty.</summary>
    /// <param name="directory">Base directory for dependency resolution; no file or directory is created.</param>
    /// <returns>A new editing session with no loose or packed save location.</returns>
    public static AudioDocument Create(string directory) =>
        new(
            new AudioDefinition
            {
                Source = AudioDefinitionSource.Generated()
            },
            filePath: null,
            directory,
            isDirty: true);

    /// <summary>Adds a loose audio reference to the selected definition with a unique resource key and marks the document dirty.</summary>
    /// <param name="path">Audio file path, normalized against the process current directory; existence is not checked.</param>
    /// <returns>The mutable resource added to the selected definition.</returns>
    /// <remarks>Stores an absolute path for unsaved or packed documents, otherwise a path relative to the document when on the same volume.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace.</exception>
    public AudioResourceDefinition AddLooseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var key = UniqueKey(Path.GetFileNameWithoutExtension(fullPath));

        var resource = new AudioResourceDefinition
        {
            Key = key,
            SourceKind = AudioResourceSourceKind.LooseFile,
            FilePath = FilePath is null
                ? fullPath
                : MakeReferencePath(fullPath, BaseDirectory),
            SourceExtension = Path.GetExtension(fullPath)
        };

        Definition.Resources.Add(resource);
        MarkChanged();
        return resource;
    }

    /// <summary>Adds a URI audio reference to the selected definition and marks the document dirty without fetching it.</summary>
    /// <param name="key">Proposed resource key, trimmed and suffixed if already in use.</param>
    /// <param name="uri">Source URI text, trimmed before storage; URI syntax is not checked here.</param>
    /// <returns>The mutable URI resource added to the selected definition.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    /// <exception cref="ArgumentException">Either argument is empty or whitespace.</exception>
    public AudioResourceDefinition AddUri(string key, string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        var resource = new AudioResourceDefinition
        {
            Key = UniqueKey(key.Trim()),
            SourceKind = AudioResourceSourceKind.Uri,
            SourceUri = uri.Trim(),
            SourceExtension = Path.GetExtension(uri)
        };

        Definition.Resources.Add(resource);
        MarkChanged();
        return resource;
    }

    /// <summary>Removes a resource from the selected definition and marks the document dirty when found.</summary>
    /// <param name="resource">The resource instance to remove; its media is not deleted.</param>
    /// <returns>True if removed; false if the resource was absent.</returns>
    public bool Remove(AudioResourceDefinition resource)
    {
        if (!Definition.Resources.Remove(resource))
            return false;

        MarkChanged();
        return true;
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
        AudioDefinitionSerializer.ValidateDocument(new AudioDefinitionDocument { Definitions = _definitions });

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
        var errors = AudioDefinitionSerializer.ValidateDocument(new() { Definitions = _definitions });
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
            var clone = AudioDefinitionSerializer.FromJson(AudioDefinitionSerializer.ToJson(member));
            clone.Source = PackedLocation is { } packed ? AudioDefinitionSource.PackedDefinitionFile(packed.Path, packed.AssetEntryName!) : AudioDefinitionSource.LooseDefinitionFile(FilePath ?? Path.Combine(BaseDirectory, "Untitled.gsnd"));
            return clone;
        }).ToList();
        AudioDefinitionSerializer.SaveAll(path, snapshots);
        var saved = AudioDefinitionSerializer.LoadAll(path);
        for (int i = 0; i < _definitions.Count; i++)
        {
            var member = _definitions[i];
            var persisted = saved[i];
            for (int j = 0; j < member.Resources.Count; j++)
            {
                member.Resources[j].FilePath = persisted.Resources[j].FilePath;
                member.Resources[j].AssetsFilePath = persisted.Resources[j].AssetsFilePath;
            }
            member.Source = AudioDefinitionSource.LooseDefinitionFile(path);
        }
        FilePath = path;
        PackedLocation = null;
        BaseDirectory = directory;
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private string UniqueKey(string proposed)
    {
        proposed = string.IsNullOrWhiteSpace(proposed) ? "audio" : proposed;
        if (Definition.Resources.All(resource =>
                !string.Equals(resource.Key, proposed, StringComparison.Ordinal)))
        {
            return proposed;
        }

        int suffix = 2;
        string candidate;
        do
        {
            candidate = $"{proposed}_{suffix++}";
        }
        while (Definition.Resources.Any(resource =>
                   string.Equals(resource.Key, candidate, StringComparison.Ordinal)));

        return candidate;
    }

    private static string MakeReferencePath(string path, string baseDirectory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullBase = Path.GetFullPath(baseDirectory);
        if (!string.Equals(
                Path.GetPathRoot(fullPath),
                Path.GetPathRoot(fullBase),
                StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        return Path.GetRelativePath(fullBase, fullPath);
    }

    private List<AudioDefinition> _definitions;

    /// <summary>Gets all logical definitions owned by this physical document.</summary>
    public IReadOnlyList<AudioDefinition> Definitions => _definitions.AsReadOnly();

    /// <summary>Gets the current logical definition without reconstructing its metadata.</summary>
    public AudioDefinition SelectedDefinition => Definition;

    /// <summary>Selects an existing member without changing the physical document's dirty state and raises <see cref="Changed"/>.</summary>
    /// <param name="definition">The member instance already owned by this document.</param>
    /// <exception cref="ArgumentException"><paramref name="definition"/> does not belong to this document.</exception>
    public void SelectDefinition(AudioDefinition definition)
    {
        if (!_definitions.Contains(definition)) throw new ArgumentException("Definition does not belong to this document.", nameof(definition));
        Definition = definition;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds and selects a new logical definition with a unique default identity.</summary>
    /// <returns>The new member.</returns>
    public AudioDefinition AddDefinition()
    {
        var definition = Create(BaseDirectory).Definition;
        definition.Key = UniqueDefinitionIdentity("audio");
        _definitions.Add(definition);
        Definition = definition;
        MarkChanged();
        return definition;
    }

    /// <summary>Duplicates and selects the current definition with a unique identity.</summary>
    /// <returns>The new independent member.</returns>
    public AudioDefinition DuplicateDefinition()
    {
        var definition = AudioDefinitionSerializer.FromJson(AudioDefinitionSerializer.ToJson(Definition));
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
    public bool RemoveDefinition(AudioDefinition definition)
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

    /// <summary>Imports selected GSND members as independent copies, rebasing dependencies without copying resources.</summary>
    /// <param name="path">A same-format source file; legacy singletons are supported.</param>
    /// <param name="select">Selects zero-based source indices. Omit to import all; return null or an empty list to cancel.</param>
    /// <returns>The number imported in source order; zero leaves the document unchanged.</returns>
    /// <remarks>Conflicting logical identities receive unique suffixes. The first imported member becomes selected and the document is marked dirty.</remarks>
    /// <exception cref="ArgumentException">The file extension is not .gsnd, or selection contains duplicate or out-of-range indices.</exception>
    public int ImportDefinitions(string path, Func<IReadOnlyList<AudioDefinition>, IReadOnlyList<int>?>? select = null)
    {
        path = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(path), ".gsnd", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a .gsnd definition document.", nameof(path));
        var sourceDirectory = Path.GetDirectoryName(path)!;
        string? Rebase(string? value) => DefinitionImport.Rebase(value, sourceDirectory, BaseDirectory);
        var imported = DefinitionImport.Prepare(path, Definitions, AudioDefinitionSerializer.LoadAll, select, member =>
        {
            var clone = AudioDefinitionSerializer.FromJson(AudioDefinitionSerializer.ToJson(member));
            foreach (var resource in clone.Resources ?? [])
            {
                resource.FilePath = Rebase(resource.FilePath);
                resource.AssetsFilePath = Rebase(resource.AssetsFilePath);
            }
            return clone;
        }, member => member.Key, (member, identity) => member.Key = identity, "audio");
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
