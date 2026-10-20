using Gondwana.Assets;

namespace Gondwana.Tooling.Tilesheets.Sources;

/// <summary>
/// Reusable, non-visual cache for browsing asset packages from authoring controls.
/// Studio may share one catalog across its GAF/GTS/GANI/GSCN surfaces.
/// </summary>
public sealed class AssetPackageCatalog : IDisposable
{
    private readonly Dictionary<string, AssetsFile> _packages =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (long Length, DateTime Modified)> _stamps = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Exception> _failures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional host callback used when a package cannot be opened without a password.
    /// Return null to cancel.
    /// </summary>
    public Func<string, string?>? PasswordProvider { get; set; }

    /// <summary>
    /// Lists the image entries available in an assets file.
    /// </summary>
    /// <param name="assetsFilePath">The path to the assets file.</param>
    /// <returns>The image entries available in the assets file.</returns>
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

    /// <summary>
    /// Opens a readable stream for a packed image.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A readable stream for the packed image. The caller must dispose the stream.</returns>
    public Stream OpenImage(PackedImageSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return OpenAsset(source.AssetsFilePath, AssetTypes.Image, source.AssetEntryName);
    }

    /// <summary>Opens an exact typed entry. No basename or different-type fallback is permitted.</summary>
    public Stream OpenAsset(string archivePath, AssetTypes type, string entryName)
    {
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

    /// <summary>
    /// Checks whether the specified image exists in its assets file.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns><see langword="true"/> if the packed image exists; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Removes cached catalog information for the specified assets file.
    /// </summary>
    /// <param name="assetsFilePath">The path to the assets file.</param>
    public void Invalidate(string assetsFilePath)
    {
        var fullPath = Path.GetFullPath(assetsFilePath);
        if (_packages.Remove(fullPath, out var package))
            package.Dispose();
        _stamps.Remove(fullPath);
        _failures.Remove(fullPath);
    }

    /// <summary>
    /// Clears the stored entries and resets the current state.
    /// </summary>
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

    /// <summary>
    /// Releases the resources owned by this instance.
    /// </summary>
    public void Dispose() => Clear();
}
