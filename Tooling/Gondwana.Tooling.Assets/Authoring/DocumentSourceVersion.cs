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
        try
        {
            // Deny in-place writers during the read. Atomic replacements are detected on the next verification.
            using var file = new FileStream(location.Path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            using var buffer = new MemoryStream();
            file.CopyTo(buffer);
            var bytes = buffer.ToArray();
            var physical = Hash(bytes);
            if (location.AssetEntryName is null) return new(DocumentSourceStatus.Available, physical, physical);
            buffer.Position = 0;
            using var archive = AssetsFile.Load(buffer, password, register: false, sourcePath: location.Path);
            using var entry = archive.Get(location.AssetType, location.AssetEntryName);
            return entry is null ? new(DocumentSourceStatus.Missing, physical, null) :
                new(DocumentSourceStatus.Available, physical, Convert.ToHexString(SHA256.HashData(entry)));
        }
        catch (FileNotFoundException) { return new(DocumentSourceStatus.Missing, null, null); }
        catch (DirectoryNotFoundException) { return new(DocumentSourceStatus.Missing, null, null); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or
            System.Security.Cryptography.CryptographicException or ICSharpCode.SharpZipLib.SharpZipBaseException)
        {
            return new(DocumentSourceStatus.Inaccessible, null, null);
        }
    }

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
        lock (SaveGate) return SaveCore(location, accepted, serialize);
    }

    private static DocumentSourceVersion SaveCore(AssetLocation location, DocumentSourceVersion accepted, Action<Action<string>> serialize)
    {
        ArgumentNullException.ThrowIfNull(serialize);
        if (location.AssetEntryName is not null) throw new ArgumentException("Use archive persistence for packed entries.", nameof(location));
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
