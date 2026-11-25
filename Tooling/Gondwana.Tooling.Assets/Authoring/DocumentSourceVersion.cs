using System.Security.Cryptography;
using Gondwana.Assets;

namespace Gondwana.Tooling.Assets;

/// <summary>The outcome of reading a physical file and, optionally, its typed entry.</summary>
public enum DocumentSourceStatus { Available, Missing, Inaccessible }

/// <summary>Immutable content fingerprints. A changed archive does not imply a changed entry.</summary>
public sealed record DocumentSourceVersion(DocumentSourceStatus Status, string? PhysicalHash, string? ContentHash)
{
    /// <summary>Reads fresh bytes without registering an archive or trusting timestamps and lengths.</summary>
    public static DocumentSourceVersion Read(AssetLocation location, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        using var batch = new ReadBatch();
        return batch.Read(location, password);
    }

    /// <summary>Shares physical reads and decompressed archive contents within one verification pass only.</summary>
    internal sealed class ReadBatch : IDisposable
    {
        private readonly Dictionary<string, DocumentSourceVersion> _loose = new(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private readonly Dictionary<(string Path, string? Password), ArchiveSnapshot> _archives = [];

        internal DocumentSourceVersion Read(AssetLocation location, string? password = null)
        {
            if (location.AssetEntryName is null)
            {
                if (!_loose.TryGetValue(location.Path, out var version))
                {
                    version = ReadLoose(location.Path);
                    _loose.Add(location.Path, version);
                }
                return version;
            }

            var key = (location.Path, password);
            if (!_archives.TryGetValue(key, out var snapshot))
            {
                snapshot = ArchiveSnapshot.Open(location.Path, password);
                _archives.Add(key, snapshot);
            }
            return snapshot.Read(location.AssetType, location.AssetEntryName);
        }

        public void Dispose()
        {
            foreach (var snapshot in _archives.Values)
                snapshot.Dispose();
            _archives.Clear();
        }
    }

    private static DocumentSourceVersion ReadLoose(string path)
    {
        try
        {
            // Loose dependencies may be large; hash directly without buffering file-sized copies.
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            var hash = Convert.ToHexString(SHA256.HashData(file));
            return new(DocumentSourceStatus.Available, hash, hash);
        }
        catch (FileNotFoundException) { return new(DocumentSourceStatus.Missing, null, null); }
        catch (DirectoryNotFoundException) { return new(DocumentSourceStatus.Missing, null, null); }
        catch (Exception ex) when (IsInaccessible(ex)) { return new(DocumentSourceStatus.Inaccessible, null, null); }
    }

    private sealed class ArchiveSnapshot : IDisposable
    {
        private readonly AssetsFile? _archive;
        private readonly HashSet<AssetsFileEntry>? _entries;
        private readonly Dictionary<AssetsFileEntry, DocumentSourceVersion> _versions = [];
        private readonly DocumentSourceStatus _status;
        private readonly string? _physicalHash;

        private ArchiveSnapshot(DocumentSourceStatus status, string? physicalHash, AssetsFile? archive)
        {
            _status = status;
            _physicalHash = physicalHash;
            _archive = archive;
            _entries = archive?.GetAllEntries().ToHashSet();
        }

        internal static ArchiveSnapshot Open(string path, string? password)
        {
            try
            {
                // Archives alone are buffered. A single path/password snapshot serves all its entries.
                using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                using var buffer = new MemoryStream();
                file.CopyTo(buffer);
                buffer.Position = 0;
                var hash = Convert.ToHexString(SHA256.HashData(buffer));
                buffer.Position = 0;
                var archive = AssetsFile.Load(buffer, password, register: false, sourcePath: path);
                return new ArchiveSnapshot(DocumentSourceStatus.Available, hash, archive);
            }
            catch (FileNotFoundException) { return new(DocumentSourceStatus.Missing, null, null); }
            catch (DirectoryNotFoundException) { return new(DocumentSourceStatus.Missing, null, null); }
            catch (Exception ex) when (IsInaccessible(ex)) { return new(DocumentSourceStatus.Inaccessible, null, null); }
        }

        internal DocumentSourceVersion Read(AssetTypes type, string name)
        {
            if (_status != DocumentSourceStatus.Available)
                return new(_status, null, null);
            var key = new AssetsFileEntry { AssetType = type, AssetName = name };
            if (_versions.TryGetValue(key, out var version))
                return version;

            // Get() allows basename fallback for regular consumers; source verification must not.
            if (!_entries!.Contains(key))
                version = new(DocumentSourceStatus.Missing, _physicalHash, null);
            else
            {
                using var entry = _archive!.Get(type, name);
                version = entry is null
                    ? new(DocumentSourceStatus.Missing, _physicalHash, null)
                    : new(DocumentSourceStatus.Available, _physicalHash, Convert.ToHexString(SHA256.HashData(entry)));
            }
            _versions.Add(key, version);
            return version;
        }

        public void Dispose() => _archive?.Dispose();
    }

    private static bool IsInaccessible(Exception ex) => ex is IOException or UnauthorizedAccessException or InvalidDataException or
        CryptographicException or ICSharpCode.SharpZipLib.SharpZipBaseException;

    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}

/// <summary>A guarded save rejected because the destination no longer matches the accepted version.</summary>
public sealed class DocumentSourceConflictException : IOException
{
    public AssetLocation Location { get; }
    public DocumentSourceVersion Current { get; }
    public DocumentSourceConflictException(AssetLocation location, DocumentSourceVersion current)
        : base($"Source changed or cannot be verified: {location}. Reload or save to a new location.")
    { Location = location; Current = current; }
}

/// <summary>Optimistic protection for existing native serializers, including their preparation interval.</summary>
public static class DocumentSourceSave
{
    // Serialize these short authoring commits across documents/monitors in this process.
    private static readonly object SaveGate = new();
    /// <summary>Rejects missing, inaccessible, or changed sources compared with the accepted version.</summary>
    public static void Verify(AssetLocation location, DocumentSourceVersion accepted, string? password = null)
    {
        var current = DocumentSourceVersion.Read(location, password);
        if (accepted.Status == DocumentSourceStatus.Inaccessible || current != accepted)
            throw new DocumentSourceConflictException(location, current);
    }

    /// <summary>Calls a serializer's SaveAll overload with a pre-install check and returns the exact installed content version.</summary>
    /// <remarks>The serializer must invoke the supplied check once immediately before installing its staged file at location.
    /// Atomic replacement is not a filesystem compare-and-swap: unrelated processes can still replace the path after the final check.</remarks>
    public static DocumentSourceVersion Save(AssetLocation location, DocumentSourceVersion accepted, Action<Action<string>> serialize)
    {
        lock (SaveGate)
            return SaveCore(location, accepted, serialize);
    }

    private static DocumentSourceVersion SaveCore(AssetLocation location, DocumentSourceVersion accepted, Action<Action<string>> serialize)
    {
        ArgumentNullException.ThrowIfNull(serialize);
        if (location.AssetEntryName is not null)
            throw new ArgumentException("Use archive persistence for packed entries.", nameof(location));
        void Check() => Verify(location, accepted);
        Check();
        using var guard = accepted.Status == DocumentSourceStatus.Available
            ? new FileStream(location.Path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete) : null;
        DocumentSourceVersion? installed = null;
        serialize(staging =>
        {
            var hash = DocumentSourceVersion.Hash(File.ReadAllBytes(staging));
            Check();
            installed = new(DocumentSourceStatus.Available, hash, hash);
        });
        return installed ?? throw new InvalidOperationException("Serializer did not invoke the pre-install check.");
    }
}
