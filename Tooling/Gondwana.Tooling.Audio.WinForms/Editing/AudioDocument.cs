using Gondwana.Audio.GSND;

namespace Gondwana.Tooling.Audio.Editing;

/// <summary>
/// UI-independent editing session for one GSND audio definition.
/// </summary>
public sealed class AudioDocument
{
    /// <summary>
    /// Gets the definition.
    /// </summary>
    public AudioDefinition Definition { get; private set; }
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

    /// <summary>
    /// Opens the supplied file for use by the audio document.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The resulting audio document.</returns>
    public static AudioDocument Open(string path)
    {
        path = Path.GetFullPath(path);
        var definitions = AudioDefinitionSerializer.LoadAll(path).ToList();
        if (definitions.Count == 0) throw new InvalidDataException("Document contains no definitions.");
        var document = new AudioDocument(definitions[0], path, Path.GetDirectoryName(path)!, false);
        document._definitions = definitions;
        return document;
    }

    /// <summary>
    /// Creates a new audio document.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The resulting audio document.</returns>
    public static AudioDocument Create(string directory) =>
        new(
            new AudioDefinition
            {
                Source = AudioDefinitionSource.Generated()
            },
            filePath: null,
            directory,
            isDirty: true);

    /// <summary>
    /// Adds loose file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The audio resource definition added for the loose file.</returns>
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

    /// <summary>
    /// Adds uri.
    /// </summary>
    /// <param name="key">The lookup key for the resource.</param>
    /// <param name="uri">The URI identifying the media source.</param>
    /// <returns>The audio resource definition added for the URI.</returns>
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

    /// <summary>
    /// Removes the selected audio resource definition from the document.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <returns><see langword="true"/> if the entry was found and removed; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Checks the definition and collects validation errors.
    /// </summary>
    /// <returns>The validation errors; an empty collection indicates that validation passed.</returns>
    public IReadOnlyList<string> Validate() =>
        AudioDefinitionSerializer.ValidateDocument(new AudioDefinitionDocument { Definitions = _definitions });

    /// <summary>
    /// Saves the current content to the destination file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="allowInvalid">Whether to save even when validation reports errors.</param>
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
            clone.Source = AudioDefinitionSource.LooseDefinitionFile(FilePath ?? Path.Combine(BaseDirectory, "Untitled.gsnd"));
            return clone;
        }).ToList();
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            AudioDefinitionSerializer.SaveAll(temporary, snapshots);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
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

    /// <summary>Selects an existing member without changing the physical document's dirty state.</summary>
    /// <param name="definition">The member to select.</param>
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

    /// <summary>Removes a member while retaining at least one editable definition.</summary>
    /// <param name="definition">The member to remove.</param>
    /// <returns>Whether the member was removed.</returns>
    public bool RemoveDefinition(AudioDefinition definition)
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

    private string UniqueDefinitionIdentity(string basis)
    {
        var candidate = basis;
        for (int i = 2; _definitions.Any(member => member.Key == candidate); i++) candidate = basis + "-" + i;
        return candidate;
    }
}
