using System.Security.Cryptography;
using Gondwana.Assets;

namespace Gondwana.Tooling.Studio.WinForms.Documents;

/// <summary>Detached reads and single-file staged replacement. Studio's browser gate serializes writers and scans.</summary>
internal sealed class StudioArchiveSnapshot : IDisposable
{
    internal AssetsFile Archive { get; }
    internal byte[] Hash { get; }
    private bool _detached;
    internal AssetsFile Detach() { _detached = true; return Archive; }

    private StudioArchiveSnapshot(AssetsFile archive, byte[] hash) { Archive = archive; Hash = hash; }

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
            throw new FileNotFoundException($"Archive entry not found: {location}");
        using var stream = archive.Get(location.AssetType, location.AssetEntryName!)!;
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    internal static void CheckVersion(string path, byte[]? expected)
    {
        if (expected is null ? File.Exists(path) : !File.Exists(path) ||
            !SHA256.HashData(File.ReadAllBytes(path)).AsSpan().SequenceEqual(expected))
            throw new IOException($"Archive changed outside this document: {path}. Reload the document to use the current archive, or Save As to retain your edits separately.");
    }

    /// <summary>Serialize only to a sibling staging file; install by atomic replacement after validation and a version recheck.</summary>
    internal static byte[] Commit(AssetsFile contents, string destination, byte[]? expected, Action? beforeInstall = null)
    {
        CheckVersion(destination, expected);
        // Deny in-place writers while staging, but permit our atomic replacement on Windows.
        using var guard = expected is null ? null : new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".gondwana-save-" + Guid.NewGuid().ToString("N") + ".gaf");
        try
        {
            using (var staged = AssetsFile.LoadOrCreate(temporary, contents.Password, contents.UseEncryption, register: false))
            {
                foreach (var entry in contents.GetAllEntries())
                {
                    using var stream = contents.Get(entry.AssetType, entry.AssetName)!;
                    staged.Add(entry.AssetType, entry.AssetName, stream);
                }
                staged.Save();
            }
            AssetsFile.Validate(temporary, contents.Password);
            using (var staged = new FileStream(temporary, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) staged.Flush(flushToDisk: true);
            var hash = SHA256.HashData(File.ReadAllBytes(temporary));
            beforeInstall?.Invoke();
            CheckVersion(destination, expected);
            if (expected is null) File.Move(temporary, destination);
            else File.Replace(temporary, destination, destinationBackupFileName: null);
            return hash;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void Dispose() { if (!_detached) Archive.Dispose(); }
}
