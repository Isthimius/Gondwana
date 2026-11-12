namespace Gondwana.Tooling.Assets;

/// <summary>Tracks physical resources read during one drop so changes during authentication or selection can be rejected.</summary>
public sealed class AuthoringResourceReadSnapshot
{
    private readonly Dictionary<string, (long Length, DateTime Modified)?> _files =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>Starts with the primary file version captured at drag initiation.</summary>
    public AuthoringResourceReadSnapshot(string path, (long Length, DateTime Modified)? stamp) => _files.Add(path, stamp);

    /// <summary>Remembers a file before reading it, or checks that a repeated read sees the same version.</summary>
    public void Observe(string path)
    {
        var info = new FileInfo(path);
        var stamp = (info.Length, info.LastWriteTimeUtc);
        if (!_files.TryAdd(path, stamp) && _files[path] != stamp)
            throw new InvalidDataException($"'{Path.GetFileName(path)}' changed during the drag. Start a new drag.");
    }

    /// <summary>Checks all observed files again immediately before committing an edit.</summary>
    public void Verify()
    {
        foreach (string path in _files.Keys)
            Observe(path);
    }
}
