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

    /// <summary>
    /// Optional host callback used when a package cannot be opened without a password.
    /// Return null to cancel.
    /// </summary>
    public Func<string, string?>? PasswordProvider { get; set; }

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

    public Stream OpenImage(PackedImageSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var fullPath = NormalizeExistingPath(source.AssetsFilePath);
        var stream = GetPackage(fullPath).Get(AssetTypes.Image, source.AssetEntryName);

        return stream ?? throw new InvalidDataException(
            $"Image asset '{source.AssetEntryName}' was not found in '{fullPath}'.");
    }

    public bool ContainsImage(PackedImageSource source)
    {
        try
        {
            using var stream = OpenImage(source);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    public void Invalidate(string assetsFilePath)
    {
        var fullPath = Path.GetFullPath(assetsFilePath);
        if (_packages.Remove(fullPath, out var package))
            package.Dispose();
    }

    public void Clear()
    {
        foreach (var package in _packages.Values)
            package.Dispose();

        _packages.Clear();
    }

    private AssetsFile GetPackage(string fullPath)
    {
        if (_packages.TryGetValue(fullPath, out var existing))
            return existing;

        AssetsFile? package = null;
        Exception? firstFailure = null;

        try
        {
            package = AssetsFile.LoadOrCreate(fullPath);
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
                throw new InvalidDataException(
                    $"Could not open asset package '{fullPath}'.",
                    firstFailure);

            try
            {
                package = AssetsFile.LoadOrCreate(
                    fullPath,
                    password,
                    encrypt: true);
                _ = package.GetAllEntries().ToList();
            }
            catch
            {
                package?.Dispose();
                throw;
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

    public void Dispose() => Clear();
}
