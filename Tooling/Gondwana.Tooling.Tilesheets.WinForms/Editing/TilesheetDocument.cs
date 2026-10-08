using System.Drawing;
using Gondwana.Drawing.Tilesheets.GTS;

namespace Gondwana.Tooling.Tilesheets.Editing;

/// <summary>
/// UI-independent editing session. The complete loaded definition remains authoritative;
/// browsing frames and changing geometry never reconstructs or prunes its metadata.
/// </summary>
public sealed class TilesheetDocument
{
    /// <summary>
    /// Gets the definition.
    /// </summary>
    public TilesheetDefinition Definition { get; }
    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string? FilePath { get; private set; }
    /// <summary>
    /// Gets the base directory.
    /// </summary>
    public string BaseDirectory { get; private set; }
    /// <summary>
    /// Gets whether the document contains unsaved changes.
    /// </summary>
    public bool IsDirty { get; private set; }
    /// <summary>
    /// Occurs when the document content changes.
    /// </summary>
    public event EventHandler? Changed;

    private TilesheetDocument(TilesheetDefinition definition, string? path, string baseDirectory)
    {
        Definition = definition;
        FilePath = path;
        BaseDirectory = Path.GetFullPath(baseDirectory);
        IsDirty = path is null;
    }

    /// <summary>
    /// Opens the supplied file for use by the tilesheet document.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The resulting tilesheet document.</returns>
    public static TilesheetDocument Open(string path)
    {
        path = Path.GetFullPath(path);
        return new(TilesheetDefinitionSerializer.Load(path), path, Path.GetDirectoryName(path)!);
    }

    /// <summary>
    /// Creates a new tilesheet document.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The resulting tilesheet document.</returns>
    public static TilesheetDocument Create(string directory) => new(new()
    {
        Name = "Untitled",
        Source = TilesheetDefinitionSource.Generated()
    }, null, directory);

    /// <summary>
    /// Marks the document as modified and notifies listeners.
    /// </summary>
    public void MarkChanged()
    {
        IsDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Resolves the tilesheet's loose image path.
    /// </summary>
    /// <returns>The requested string, or <see langword="null"/> when unavailable.</returns>
    public string? ResolveImagePath() => string.IsNullOrWhiteSpace(Definition.Image?.FilePath)
        ? null : Path.GetFullPath(Definition.Image.FilePath, BaseDirectory);

    /// <summary>
    /// Resolves the assets file path for the tilesheet's packed image.
    /// </summary>
    /// <returns>The requested string, or <see langword="null"/> when unavailable.</returns>
    public string? ResolveAssetsFilePath() => string.IsNullOrWhiteSpace(Definition.Image?.AssetsFilePath)
        ? null : Path.GetFullPath(Definition.Image.AssetsFilePath, BaseDirectory);

    /// <summary>
    /// Sets a loose image file as the tilesheet source.
    /// </summary>
    /// <param name="path">The path.</param>
    public void SetImage(string path)
    {
        // Changing the source is explicit. Unrelated edits never clear packed fields.
        Definition.Image = new() { FilePath = path };
        MarkChanged();
    }

    /// <summary>
    /// Sets an image entry in an assets file as the tilesheet source.
    /// </summary>
    /// <param name="assetsFilePath">The path to the assets file.</param>
    /// <param name="assetEntryName">The entry name inside the assets file.</param>
    public void SetPackedImage(string assetsFilePath, string assetEntryName)
    {
        if (string.IsNullOrWhiteSpace(assetsFilePath))
            throw new ArgumentException("Assets file path must be a non-empty string.", nameof(assetsFilePath));
        if (string.IsNullOrWhiteSpace(assetEntryName))
            throw new ArgumentException("Asset entry name must be a non-empty string.", nameof(assetEntryName));

        Definition.Image = new()
        {
            AssetsFilePath = Path.GetFullPath(assetsFilePath),
            AssetEntryName = assetEntryName
        };
        MarkChanged();
    }

    /// <summary>
    /// Checks the definition and collects validation errors.
    /// </summary>
    /// <param name="imageSize">The source image size used to validate frame bounds, when known.</param>
    /// <returns>The validation errors; an empty collection indicates that validation passed.</returns>
    public IReadOnlyList<string> Validate(Size? imageSize = null)
    {
        var errors = TilesheetDefinitionValidator.Validate(Definition, imageSize?.Width, imageSize?.Height).ToList();
        // The shared validator owns all geometry rules. These are loose-file I/O checks
        // (the core validator deliberately does not access the filesystem).
        var image = Definition.Image;
        if (image is null)
            errors.Add("Image source is missing.");
        else if (!string.IsNullOrWhiteSpace(image.FilePath))
        {
            if (!string.IsNullOrWhiteSpace(image.AssetsFilePath) || !string.IsNullOrWhiteSpace(image.AssetEntryName))
                errors.Add("Image source is ambiguous: loose and packed sources are both specified.");
            try
            {
                if (!File.Exists(ResolveImagePath())) errors.Add("Image file does not exist: " + image.FilePath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
            {
                errors.Add("Invalid image path: " + ex.Message);
            }
        }
        else if (string.IsNullOrWhiteSpace(image.AssetsFilePath) || string.IsNullOrWhiteSpace(image.AssetEntryName))
            errors.Add("Specify a loose image file (or a complete existing packed image source).");
        return errors;
    }

    /// <summary>
    /// Finds the stored metadata for a frame in the selected region.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <returns>The requested tilesheet frame definition, or <see langword="null"/> when unavailable.</returns>
    public TilesheetFrameDefinition? FindFrame(TilesheetRegionDefinition region, int x, int y) =>
        region.Frames.FirstOrDefault(f => f is not null && f.XTile == x && f.YTile == y);

    /// <summary>
    /// Gets or creates editable metadata for a frame in the selected region.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <returns>The editable frame metadata, created when none was previously stored.</returns>
    public TilesheetFrameDefinition EditFrame(TilesheetRegionDefinition region, int x, int y)
    {
        var frame = FindFrame(region, x, y);
        if (frame is not null) return frame;
        frame = new() { XTile = x, YTile = y };
        region.Frames.Add(frame);
        return frame;
    }

    /// <summary>
    /// Removes frame metadata whose coordinates fall outside the region grid.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <returns>The number of frame metadata entries removed.</returns>
    public int RemoveOutOfGridMetadata(TilesheetRegionDefinition region)
    {
        var (columns, rows) = TilesheetDefinitionValidator.GridSize(region);
        int removed = region.Frames.RemoveAll(f => f is not null &&
            (f.XTile < 0 || f.YTile < 0 || f.XTile >= columns || f.YTile >= rows));
        if (removed > 0) MarkChanged();
        return removed;
    }

    /// <summary>
    /// Saves the current content to the destination file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="imageSize">The source image size used to validate frame bounds, when known.</param>
    /// <param name="allowInvalid">Whether to save even when validation reports errors.</param>
    public void Save(string path, Size? imageSize = null, bool allowInvalid = false)
    {
        var errors = Validate(imageSize);
        if (errors.Count > 0 && !allowInvalid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

        path = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(path)!;
        // Snapshot through the official serializer, never a hand-maintained subset.
        var snapshot = TilesheetDefinitionSerializer.FromJson(TilesheetDefinitionSerializer.ToJson(Definition));
        if (snapshot.Image is not null)
        {
            snapshot.Image.FilePath = Rebase(snapshot.Image.FilePath, directory);
            snapshot.Image.AssetsFilePath = Rebase(snapshot.Image.AssetsFilePath, directory);
        }
        if (snapshot.Source.Kind == TilesheetDefinitionSourceKind.None)
            snapshot.Source = TilesheetDefinitionSource.LooseDefinitionFile(path);

        // Write beside the destination and replace only after serialization succeeds.
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            TilesheetDefinitionSerializer.Save(temporary, snapshot);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        Definition.Image = snapshot.Image!;
        Definition.Source = snapshot.Source;
        FilePath = path;
        BaseDirectory = directory;
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private string? Rebase(string? path, string directory)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        // Preserve existing relative spelling on ordinary Save.
        if (!Path.IsPathRooted(path) && string.Equals(directory, BaseDirectory, StringComparison.OrdinalIgnoreCase))
            return path;
        return Path.GetRelativePath(directory, Path.GetFullPath(path, BaseDirectory)).Replace('\\', '/');
    }
}
