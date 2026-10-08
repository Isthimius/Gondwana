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
    public AudioDefinition Definition { get; }
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
        Definition = definition;
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
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = Path.GetFullPath(path);

        return new AudioDocument(
            AudioDefinitionSerializer.Load(path),
            path,
            Path.GetDirectoryName(path)!,
            isDirty: false);
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
        AudioDefinitionValidator.Validate(Definition);

    /// <summary>
    /// Saves the current content to the destination file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="allowInvalid">Whether to save even when validation reports errors.</param>
    public void Save(string path, bool allowInvalid = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var errors = Validate();
        if (errors.Count > 0 && !allowInvalid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

        path = Path.GetFullPath(path);
        AudioDefinitionSerializer.Save(path, Definition);

        // Reload the official persisted representation so Save As rebasing and
        // provenance are reflected by the in-memory document as well.
        var saved = AudioDefinitionSerializer.Load(path);
        Definition.Resources = saved.Resources;
        Definition.Source = saved.Source;

        FilePath = path;
        BaseDirectory = Path.GetDirectoryName(path)!;
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
}
