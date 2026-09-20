using Gondwana.Audio.GSND;

namespace Gondwana.Tooling.Audio.Editing;

/// <summary>
/// UI-independent editing session for one GSND audio definition.
/// </summary>
public sealed class AudioDocument
{
    public AudioDefinition Definition { get; }
    public string? FilePath { get; private set; }
    public string BaseDirectory { get; private set; }
    public bool IsDirty { get; private set; }
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

    public static AudioDocument Create(string directory) =>
        new(
            new AudioDefinition
            {
                Source = AudioDefinitionSource.Generated()
            },
            filePath: null,
            directory,
            isDirty: true);

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

    public bool Remove(AudioResourceDefinition resource)
    {
        if (!Definition.Resources.Remove(resource))
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
        AudioDefinitionValidator.Validate(Definition);

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
