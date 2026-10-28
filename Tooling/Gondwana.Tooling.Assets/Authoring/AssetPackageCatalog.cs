using Gondwana.Assets;

namespace Gondwana.Tooling.Tilesheets.Sources;

/// <summary>
/// Reusable, non-visual cache for browsing asset packages from authoring controls.
/// Studio may share one catalog across its GAF/GTS/GANI/GSCN surfaces.
/// </summary>
public sealed class AssetPackageCatalog : IDisposable
{
    /// <summary>Reports a physical package dependency so hosts can indicate stale previews after writes.</summary>
    public event Action<string>? PackageAccessed;
    /// <summary>Reports a successfully authenticated password for an optional host session cache.</summary>
    public event Action<string, string>? PasswordAccepted;
    private readonly Dictionary<string, AssetsFile> _packages =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (long Length, DateTime Modified)> _stamps = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Exception> _failures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets a callback receiving the full archive path after an unauthenticated load fails.</summary>
    /// <remarks>Return a password for one retry, or null to decline. Failures are cached until invalidation or a file length/timestamp change. Invoked synchronously on the caller's thread.</remarks>
    public Func<string, string?>? PasswordProvider { get; set; }

    /// <summary>Lists exact entry identities of the requested stored type without extracting files.</summary>
    public IReadOnlyList<Gondwana.Tooling.Assets.AuthoringResource> GetResources(string archivePath, AssetTypes type)
    {
        var fullPath = NormalizeExistingPath(archivePath);
        PackageAccessed?.Invoke(fullPath);
        return GetPackage(fullPath).GetAllEntries()
            .Where(entry => entry.AssetType == type)
            .OrderBy(entry => entry.AssetName, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new Gondwana.Tooling.Assets.AuthoringResource(fullPath, type, entry.AssetName))
            .ToArray();
    }

    /// <summary>Lists image entries from a cached or newly opened package, sorted by entry name without case.</summary>
    /// <param name="assetsFilePath">Existing archive path; relative paths use the process current directory.</param>
    /// <returns>A new list of image identities without extracting their contents.</returns>
    /// <exception cref="FileNotFoundException">The archive does not exist.</exception>
    /// <exception cref="InvalidDataException">The archive cannot be loaded, including when authentication is declined or fails.</exception>
    public IReadOnlyList<PackedImageSource> GetImages(string assetsFilePath)
    {
        var fullPath = NormalizeExistingPath(assetsFilePath);
        var package = GetPackage(fullPath);

        return package.GetAllEntries()
            .Where(entry => entry.AssetType == AssetTypes.Image)
            .OrderBy(entry => entry.AssetName, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new PackedImageSource(fullPath, entry.AssetName))
            .ToArray();
    }

    /// <summary>Opens the exact image entry from the package cache, requesting credentials if needed.</summary>
    /// <param name="source">Archive path and exact image entry name.</param>
    /// <returns>A readable stream owned by the caller, which must dispose it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="InvalidDataException">The archive cannot be loaded or the exact image entry is unavailable.</exception>
    public Stream OpenImage(PackedImageSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return OpenAsset(source.AssetsFilePath, AssetTypes.Image, source.AssetEntryName);
    }

    /// <summary>Opens an exact typed entry without basename or different-type fallback and reports the package dependency.</summary>
    /// <param name="archivePath">Existing archive path, normalized against the process current directory.</param>
    /// <param name="type">Required stored asset type.</param>
    /// <param name="entryName">Full entry name matched without case within that type.</param>
    /// <returns>A readable entry stream owned by the caller, which must dispose it.</returns>
    /// <exception cref="FileNotFoundException">The archive does not exist.</exception>
    /// <exception cref="InvalidDataException">The package cannot be loaded or the exact typed entry cannot be read.</exception>
    public Stream OpenAsset(string archivePath, AssetTypes type, string entryName)
    {
        PackageAccessed?.Invoke(Path.GetFullPath(archivePath));
        var fullPath = NormalizeExistingPath(archivePath);
        var package = GetPackage(fullPath);

        // AssetsFile.Get intentionally falls back to base-name matching when an
        // exact name is absent. Persistent GTS references must not use that fallback:
        // "forest.png" must never silently resolve to "forest.jpg".
        var exactEntry = package.GetAllEntries().FirstOrDefault(entry =>
            entry.AssetType == type &&
            string.Equals(
                entry.AssetName,
                entryName,
                StringComparison.OrdinalIgnoreCase));

        if (exactEntry is null)
        {
            throw new InvalidDataException(
                $"{type} asset '{entryName}' was not found in '{fullPath}' with the expected type.");
        }

        return package.Get(type, exactEntry.AssetName)
            ?? throw new InvalidDataException(
                $"{type} asset '{entryName}' could not be read from '{fullPath}'.");
    }

    /// <summary>Attempts to open and dispose an exact image entry, using the cache and password callback as needed.</summary>
    /// <param name="source">Archive path and exact image entry name to probe.</param>
    /// <returns>True if the stream opens; false for handled path, access, load, or entry errors. Image pixels are not decoded.</returns>
    public bool ContainsImage(PackedImageSource source)
    {
        try
        {
            using var stream = OpenImage(source);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
            InvalidDataException or
            ArgumentException or
            UnauthorizedAccessException or
            NotSupportedException or
            InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Disposes the cached package and forgets its file stamp and cached failure.</summary>
    /// <param name="assetsFilePath">Archive path to invalidate, normalized against the process current directory.</param>
    public void Invalidate(string assetsFilePath)
    {
        var fullPath = Path.GetFullPath(assetsFilePath);
        if (_packages.Remove(fullPath, out var package))
            package.Dispose();
        _stamps.Remove(fullPath);
        _failures.Remove(fullPath);
    }

    /// <summary>Disposes all cached packages and clears file stamps and failures, retaining callbacks and subscribers.</summary>
    public void Clear()
    {
        foreach (var package in _packages.Values)
            package.Dispose();

        _packages.Clear();
        _stamps.Clear();
        _failures.Clear();
    }

    private AssetsFile GetPackage(string fullPath)
    {
        var info = new FileInfo(fullPath);
        var stamp = (info.Length, info.LastWriteTimeUtc);
        if (_stamps.TryGetValue(fullPath, out var previous) && previous != stamp)
            Invalidate(fullPath);
        _stamps[fullPath] = stamp;
        if (_failures.TryGetValue(fullPath, out var failure))
            throw new InvalidDataException($"Could not open asset package '{fullPath}': {failure.Message}", failure);
        if (_packages.TryGetValue(fullPath, out var existing))
            return existing;

        AssetsFile? package = null;
        Exception? firstFailure = null;

        try
        {
            using var stream = File.OpenRead(fullPath);
            package = AssetsFile.Load(stream, password: null, register: false, sourcePath: fullPath);
            _ = package.GetAllEntries().ToList();
        }
        catch (Exception ex)
        {
            firstFailure = ex;
            package?.Dispose();
            package = null;
        }

        if (package is null)
        {
            var password = PasswordProvider?.Invoke(fullPath);
            if (password is null)
            {
                _failures[fullPath] = firstFailure!;
                throw new InvalidDataException(
                    $"Could not open asset package '{fullPath}' (corrupt, unreadable, or password-protected): {firstFailure?.Message}",
                    firstFailure);
            }

            try
            {
                using var stream = File.OpenRead(fullPath);
                package = AssetsFile.Load(stream, password, register: false, sourcePath: fullPath);
                _ = package.GetAllEntries().ToList();
            }
            catch (Exception ex)
            {
                package?.Dispose();
                _failures[fullPath] = ex;
                throw new InvalidDataException($"Could not open asset package '{fullPath}' with the supplied password: {ex.Message}", ex);
            }
            PasswordAccepted?.Invoke(fullPath, password);
        }

        _packages.Add(fullPath, package);
        return package;
    }

    private static string NormalizeExistingPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(
                "Asset package path must be a non-empty string.",
                nameof(path));

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException(
                $"Asset package not found: {fullPath}",
                fullPath);

        return fullPath;
    }

    /// <summary>Clears and disposes cached packages; the catalog holds no runtime registrations.</summary>
    public void Dispose() => Clear();
}
