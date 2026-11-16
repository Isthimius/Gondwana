using System.Security.Cryptography;
using Gondwana.Assets;
using Gondwana.Tooling.Assets;

namespace Gondwana.Tooling.Studio.WinForms.Documents;

/// <summary>Detached reads and single-file staged replacement. Studio's browser gate serializes writers and scans.</summary>
internal sealed class StudioArchiveSnapshot : IDisposable
{
    internal AssetsFile Archive { get; }
    internal byte[] Hash { get; }
    private bool _detached;

    internal AssetsFile Detach()
    {
        _detached = true;
        return Archive;
    }

    private StudioArchiveSnapshot(AssetsFile archive, byte[] hash)
    {
        Archive = archive;
        Hash = hash;
    }

    internal static StudioArchiveSnapshot Open(string path, string? password)
    {
        var bytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes, writable: false);

        return new(AssetsFile.Load(stream, password, register: false, sourcePath: path), SHA256.HashData(bytes));
    }

    internal static byte[] Entry(AssetsFile archive, AssetLocation location)
    {
        if (!archive.GetAllEntries().Any(entry => entry.AssetType == location.AssetType &&
            string.Equals(entry.AssetName, location.AssetEntryName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new FileNotFoundException($"Archive entry not found: {location}");
        }

        using var stream = archive.Get(location.AssetType, location.AssetEntryName!)!;
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);

        return bytes.ToArray();
    }

    internal static void CheckVersion(string path, byte[]? expected) => AssetArchivePersistence.CheckVersion(path, expected);

    /// <summary>Uses the shared staged persistence path without changing the editor's loaded buffers.</summary>
    internal static byte[] Commit(AssetsFile contents, string destination, byte[]? expected, Action? beforeInstall = null) =>
        AssetArchivePersistence.Commit(contents, destination, expected, beforeInstall);

    /// <summary>Disposes the snapshot archive unless ownership has been transferred by <see cref="Detach"/>.</summary>
    public void Dispose()
    {
        if (!_detached)
            Archive.Dispose();
    }
}
