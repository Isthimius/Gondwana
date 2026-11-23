using Gondwana.Assets;

namespace Gondwana.Tooling.Assets;

/// <summary>Walks explicit references in existing native documents, including all logical members.</summary>
public static class DocumentSourceDependencies
{
    /// <summary>Physical paths follow platform comparison; packed entries additionally retain their type and name.</summary>
    public static IEqualityComparer<AssetLocation> Comparer { get; } = new LocationComparer();

    /// <summary>Enumerates native payloads already loaded by an archive editor without serializing the archive.</summary>
    public static IEnumerable<(AssetLocation Location, byte[] Bytes)> ArchiveRoots(AssetsFile archive)
    {
        foreach (var entry in archive.GetAllEntries().Where(e => PackDefinitionDocument.IsNative(e.AssetType)))
        {
            using var stream = archive.Get(entry.AssetType, entry.AssetName)!;
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            yield return (new(entry.AssetType, archive.SourcePath!, entry.AssetName), bytes.ToArray());
        }
    }

    /// <summary>Uses the shared reference resolver. Partial results survive malformed drafts through observed;
    /// versions correspond to bytes actually traversed. Password lookup must not prompt from a worker thread.</summary>
    public static IEnumerable<AssetLocation> Discover(AssetLocation owner,
        IEnumerable<(AssetLocation Location, byte[] Bytes)> roots, bool archiveOwner,
        Func<string, string?> password, IDictionary<AssetLocation, DocumentSourceVersion> versions,
        ISet<AssetLocation> observed)
    {
        var pending = new Queue<(AssetLocation Location, byte[] Bytes)>(roots);
        var visited = new HashSet<AssetLocation>(pending.Select(p => p.Location), Comparer) { owner };
        Exception? incomplete = null;
        while (pending.TryDequeue(out var item))
        {
            var references = new List<AssetLocation>();
            try
            {
                references.AddRange(PackDefinitionDocument.Read(item.Location.AssetType, item.Bytes,
                    item.Location.AssetEntryName ?? item.Location.Path)?.References(item.Location) ?? []);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or InvalidOperationException or
                NullReferenceException or Newtonsoft.Json.JsonException) { incomplete = ex; }
            foreach (var reference in references)
            {
                if (!visited.Add(reference)) continue;
                if (!archiveOwner || !PathEquals(reference.Path, owner.Path))
                {
                    observed.Add(reference);
                    yield return reference;
                }
                if (!PackDefinitionDocument.IsNative(reference.AssetType)) continue;
                byte[] bytes;
                try
                {
                    var physical = File.ReadAllBytes(reference.Path);
                    var hash = DocumentSourceVersion.Hash(physical);
                    if (reference.AssetEntryName is null) bytes = physical;
                    else
                    {
                        using var stream = new MemoryStream(physical, writable: false);
                        using var archive = AssetsFile.Load(stream, password(reference.Path), register: false, sourcePath: reference.Path);
                        if (!archive.GetAllEntries().Any(e => e.AssetType == reference.AssetType &&
                            StringComparer.OrdinalIgnoreCase.Equals(e.AssetName, reference.AssetEntryName)))
                            throw new FileNotFoundException($"Referenced typed entry is missing: {reference}");
                        using var entry = archive.Get(reference.AssetType, reference.AssetEntryName)!;
                        using var content = new MemoryStream();
                        entry.CopyTo(content);
                        bytes = content.ToArray();
                    }
                    versions[reference] = new(DocumentSourceStatus.Available, hash, DocumentSourceVersion.Hash(bytes));
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                    System.Security.Cryptography.CryptographicException or ICSharpCode.SharpZipLib.SharpZipBaseException)
                {
                    // The host retains its last complete inventory while a native dependency cannot
                    // be traversed. Dropping its transitive image/media subscriptions would hide changes.
                    incomplete = ex;
                    continue;
                }
                pending.Enqueue((reference, bytes));
            }
        }
        if (incomplete is not null) throw new IOException("Some dependency references could not be read; retain the previous inventory.", incomplete);
    }

    private static bool PathEquals(string left, string right) => Paths.Equals(left, right);
    private static StringComparer Paths => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private sealed class LocationComparer : IEqualityComparer<AssetLocation>
    {
        public bool Equals(AssetLocation? x, AssetLocation? y) => ReferenceEquals(x, y) || x is not null && y is not null &&
            PathEquals(x.Path, y.Path) && StringComparer.OrdinalIgnoreCase.Equals(x.AssetEntryName, y.AssetEntryName) &&
            (x.AssetEntryName is null || x.AssetType == y.AssetType);
        public int GetHashCode(AssetLocation obj) => HashCode.Combine(Paths.GetHashCode(obj.Path),
            obj.AssetEntryName is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.AssetEntryName),
            obj.AssetEntryName is null ? 0 : (int)obj.AssetType);
    }
}
