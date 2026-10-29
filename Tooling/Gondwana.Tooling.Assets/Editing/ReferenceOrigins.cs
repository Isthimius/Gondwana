using System.Runtime.CompilerServices;

namespace Gondwana.Tooling.Editing;

/// <summary>Remembers the path base of retained reference instances across Save As, without extending their lifetime.</summary>
public sealed class ReferenceOrigins
{
    private sealed record Origin(string Directory, string? Archive);
    private readonly ConditionalWeakTable<object, Origin> _origins = new();

    /// <summary>Records the current interpretation of a reference after loading or successfully saving it.</summary>
    public void Remember(object reference, string directory, string? archive)
    {
        _origins.Remove(reference);
        _origins.Add(reference, new(directory, archive));
    }

    /// <summary>Rebases a reinstated reference, including formerly implicit references into its own archive.</summary>
    public void Restore(object reference, string directory, string? archive, string? entry,
        Func<string?> getLoose, Action<string?> setLoose, Func<string?> getArchive, Action<string?> setArchive)
    {
        if (_origins.TryGetValue(reference, out var origin))
        {
            string? Rebase(string? path)
            {
                if (string.IsNullOrWhiteSpace(path) || string.Equals(directory, origin.Directory, StringComparison.OrdinalIgnoreCase)) return path;
                try { return Path.GetRelativePath(directory, Path.GetFullPath(path, origin.Directory)).Replace('\\', '/'); }
                // Invalid draft references remain invalid and visible to document validation, but cannot break replay.
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { return path; }
            }
            setLoose(Rebase(getLoose()));
            var packed = getArchive();
            if (string.IsNullOrWhiteSpace(packed) && !string.IsNullOrWhiteSpace(entry) && origin.Archive is not null &&
                !string.Equals(archive, origin.Archive, StringComparison.OrdinalIgnoreCase))
                packed = origin.Archive;
            setArchive(Rebase(packed));
        }
        Remember(reference, directory, archive);
    }
}
