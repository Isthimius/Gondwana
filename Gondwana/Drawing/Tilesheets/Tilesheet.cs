using System.Drawing;
using Gondwana.Assets;
using Gondwana.SkiaSharp;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SkiaSharp;

namespace Gondwana.Drawing.Tilesheets;

/// <summary>
/// Represents a tilesheet image and metadata for rendering tiles.
/// </summary>
[JsonObject(IsReference = true)]
public sealed class Tilesheet : IDisposable
{
    /// <summary>
    /// Occurs when this tilesheet is disposed.
    /// </summary>
    public event Action? Disposed;

    #region ctors

    private Tilesheet()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tilesheet"/> class with the specified name and bitmap.
    /// </summary>
    /// <param name="name">The name to assign to this tilesheet.</param>
    /// <param name="bitmap">The SkiaSharp bitmap containing the tilesheet image.</param>
    public Tilesheet(string name, SKBitmap bitmap)
        : this()
    {
        _name = name;
        SkBitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        TilesheetRegistry.Instance.Register(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tilesheet"/> class by loading an image from a stream.
    /// </summary>
    /// <param name="name">The name to assign to this tilesheet.</param>
    /// <param name="stream">The stream containing the image data.</param>
    /// <exception cref="ArgumentException">Thrown when the stream contains invalid image data.</exception>
    public Tilesheet(string name, Stream stream)
        : this(name, SKBitmap.Decode(stream) ?? throw new ArgumentException("Invalid image stream.")) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tilesheet"/> class by loading an image from a file.
    /// </summary>
    /// <param name="name">The name to assign to this tilesheet.</param>
    /// <param name="file">The path to the image file.</param>
    /// <exception cref="ArgumentException">Thrown when the file is not a valid image.</exception>
    public Tilesheet(string name, string file)
        : this(name, SKBitmap.Decode(file) ?? throw new ArgumentException($"Invalid image file: {file}"))
    {
        ImageFilePath = file;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tilesheet"/> class by loading an image from an assets file.
    /// </summary>
    /// <param name="resFile">The assets file containing the tilesheet image.</param>
    /// <param name="entryName">The name of the asset entry within the assets file.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resFile"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entryName"/> is null or whitespace, or when the asset cannot be decoded.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the asset entry does not exist or returns a null data stream.</exception>
    public Tilesheet(AssetsFile resFile, string entryName)
    {
        if (resFile is null)
            throw new ArgumentNullException(nameof(resFile));

        if (string.IsNullOrWhiteSpace(entryName))
            throw new ArgumentException("Asset entry name must be a non-empty string.", nameof(entryName));

        AssetIdentifier = new AssetsFileIdentifier(resFile, AssetTypes.Image, entryName);

        using var assetStream = AssetIdentifier.Data
            ?? throw new InvalidOperationException(
                $"Tilesheet asset '{entryName}' could not be loaded from AssetsFile '{resFile.FilePath}'. " +
                "The asset entry does not exist or returned a null data stream."
            );

        SkBitmap = SKBitmap.Decode(assetStream)
            ?? throw new ArgumentException(
                $"Failed to decode tilesheet bitmap from asset '{entryName}' in AssetsFile '{resFile.FilePath}'. " +
                "The asset data is corrupt or not a supported image format."
            );

        _name = entryName;

        // Register AFTER successful decode so the registry never contains a half-constructed tilesheet
        TilesheetRegistry.Instance.Register(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tilesheet"/> class by copying settings from a base tilesheet
    /// and loading a new image from a file.
    /// </summary>
    /// <param name="baseSheet">The tilesheet whose settings should be copied.</param>
    /// <param name="name">The name to assign to this tilesheet.</param>
    /// <param name="file">The path to the image file.</param>
    public Tilesheet(Tilesheet baseSheet, string name, string file)
    {
        if (baseSheet is null)
            throw new ArgumentNullException(nameof(baseSheet));

        _name = name;

        SkBitmap = SKBitmap.Decode(file)
            ?? throw new ArgumentException($"Invalid image file: {file}");

        ImageFilePath = file;
        ValueBag = new(baseSheet.ValueBag);

        foreach (var region in baseSheet.Regions)
        {
            AddRegion(
                region.Name,
                region.Area,
                region.Spacing,
                region.TileSize,
                region.OverhangPixels);
        }

        TilesheetRegistry.Instance.Register(this);
    }

    #endregion ctors

    /// <summary>
    /// Gets the SkiaSharp bitmap containing the tilesheet image.
    /// This may be a modified version if alpha masking or premultiplication has been applied.
    /// </summary>
    [JsonIgnore]
    public SKBitmap SkBitmap { get; private set; } = null!;

    /// <summary>
    /// Gets the original SkiaSharp bitmap before any alpha masking or premultiplication was applied.
    /// Returns <see langword="null"/> if no modifications have been made.
    /// </summary>
    [JsonIgnore]
    public SKBitmap? SkBitmapOriginal { get; private set; } = null;

    [JsonProperty("name")]
    private string _name = string.Empty;

    /// <summary>
    /// Gets or sets the name of this tilesheet.
    /// Changing the name updates the tilesheet's registration in the <see cref="TilesheetRegistry"/>.
    /// </summary>
    [JsonIgnore]
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
                return;

            var old = _name;
            _name = value;
            TilesheetRegistry.Instance.OnTilesheetRenamed(old, _name, this);
        }
    }

    /// <summary>
    /// Gets the regions that define tile layouts within this tilesheet.
    /// Each region may define its own area, tile size, spacing, and overhang settings.
    /// </summary>
    [JsonProperty("regions")]
    public List<TilesheetRegion> Regions { get; private set; } = new();

    /// <summary>
    /// Gets or sets the value bag for storing arbitrary typed values associated with this tilesheet.
    /// </summary>
    [JsonIgnore]
    public TypedValueBag ValueBag { get; set; } = new();

    /// <summary>
    /// Gets the asset identifier if this tilesheet was loaded from an assets file.
    /// Returns <see langword="null"/> if the tilesheet was loaded from another source.
    /// </summary>
    [JsonProperty]
    public AssetsFileIdentifier? AssetIdentifier { get; private set; }

    /// <summary>
    /// Gets the file path of the image file if this tilesheet was loaded from a file.
    /// Returns an empty string if the tilesheet was loaded from another source.
    /// </summary>
    [JsonProperty]
    public string ImageFilePath { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the color used for alpha masking, if <see cref="ApplyMask"/> has been called.
    /// Returns <see langword="null"/> if no mask has been applied.
    /// </summary>
    [JsonProperty]
    public SKColor? MaskColor { get; private set; } = null;

    /// <summary>
    /// Gets the tolerance value used when applying the alpha mask.
    /// This determines how closely pixels must match the mask color to be made transparent.
    /// </summary>
    [JsonProperty]
    public byte MaskTolerance { get; private set; } = 5;

    /// <summary>
    /// Gets a value indicating whether the bitmap has been premultiplied with its alpha channel.
    /// This is <see langword="true"/> after calling <see cref="ApplyMask"/> or <see cref="ApplyPremultiplyAlpha"/>.
    /// </summary>
    [JsonProperty]
    public bool Premultiplied { get; private set; } = false;

    #region Methods

    /// <summary>
    /// Adds a region to this tilesheet.
    /// </summary>
    /// <param name="name">The name to assign to this region.</param>
    /// <param name="area">The rectangular area of the source image occupied by this region.</param>
    /// <param name="spacing">The horizontal and vertical spacing between tiles in this region.</param>
    /// <param name="tileSize">The size of each individual tile in this region.</param>
    /// <param name="overhangPixels">The overhang dimensions for tiles in this region.</param>
    /// <returns>The newly created <see cref="TilesheetRegion"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the region name is null, whitespace, or already exists.</exception>
    public TilesheetRegion AddRegion(
        string name,
        Rectangle area,
        Size spacing,
        Size tileSize,
        Overhang overhangPixels)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Region name must be a non-empty string.", nameof(name));

        if (GetRegion(name) != null)
            throw new ArgumentException($"A tilesheet region named '{name}' already exists.", nameof(name));

        var region = new TilesheetRegion(
            this,
            name,
            area,
            spacing,
            tileSize,
            overhangPixels);

        Regions.Add(region);

        return region;
    }

    /// <summary>
    /// Adds a region to this tilesheet.
    /// </summary>
    /// <param name="name">The name to assign to this region.</param>
    /// <param name="area">The rectangular area of the source image occupied by this region.</param>
    /// <param name="spacing">The horizontal and vertical spacing between tiles in this region.</param>
    /// <param name="tileSize">The size of each individual tile in this region.</param>
    /// <returns>The newly created <see cref="TilesheetRegion"/>.</returns>
    public TilesheetRegion AddRegion(
        string name,
        Rectangle area,
        Size spacing,
        Size tileSize)
    {
        return AddRegion(name, area, spacing, tileSize, Overhang.None);
    }

    /// <summary>
    /// Retrieves the tilesheet region with the specified name.
    /// </summary>
    /// <param name="name">The name of the region to retrieve.</param>
    /// <returns>
    /// The matching <see cref="TilesheetRegion"/>, or <see langword="null"/> if no matching region exists.
    /// </returns>
    public TilesheetRegion? GetRegion(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        foreach (var region in Regions)
        {
            if (string.Equals(region.Name, name, StringComparison.OrdinalIgnoreCase))
                return region;
        }

        return null;
    }

    /// <summary>
    /// Removes the tilesheet region with the specified name.
    /// </summary>
    /// <param name="name">The name of the region to remove.</param>
    /// <param name="dispose">Whether the removed region should be disposed.</param>
    /// <returns>
    /// <see langword="true"/> if a matching region was found and removed; otherwise, <see langword="false"/>.
    /// </returns>
    public bool RemoveRegion(string name, bool dispose = true)
    {
        var region = GetRegion(name);

        if (region == null)
            return false;

        Regions.Remove(region);

        if (dispose)
            region.Dispose();

        return true;
    }

    /// <summary>
    /// Applies an alpha mask to the tilesheet, making pixels matching the specified color transparent,
    /// and then premultiplies the alpha channel.
    /// </summary>
    /// <param name="maskColor">The color to treat as transparent. If <see langword="null"/>, defaults to white.</param>
    /// <param name="tolerance">The tolerance for color matching (0-255). Lower values require closer matches.</param>
    /// <exception cref="ArgumentException">Thrown when the bitmap is null or empty.</exception>
    public void ApplyMask(SKColor? maskColor = null, byte tolerance = 5)
    {
        if (SkBitmap == null || SkBitmap.IsEmpty)
            throw new ArgumentException("Invalid bitmap.");

        var targetColor = maskColor ?? SKColors.White;

        MaskColor = targetColor;
        MaskTolerance = tolerance;
        Premultiplied = true;

        ClearTileCache();

        // ensure the bitmap actually supports alpha
        if (SkBitmap.Info.AlphaType == SKAlphaType.Opaque)
        {
            var info = new SKImageInfo(
                SkBitmap.Width,
                SkBitmap.Height,
                SkBitmap.Info.ColorType,
                SKAlphaType.Premul
            );

            var withAlpha = new SKBitmap(info);
            using (var canvas = new SKCanvas(withAlpha))
            {
                canvas.DrawBitmap(SkBitmap, 0, 0);
            }

            SkBitmap.Dispose();
            SkBitmap = withAlpha;
        }

        SkBitmapOriginal?.Dispose();
        SkBitmapOriginal = SkBitmap.Copy();

        SkBitmap.ApplyAlphaMask(targetColor, tolerance);
        SkBitmap = SkBitmap.PremultiplyAlpha();

        BuildTileCache();
    }

    /// <summary>
    /// Premultiplies the alpha channel of the bitmap, improving rendering performance and quality.
    /// Preserves the original bitmap in <see cref="SkBitmapOriginal"/> before applying the operation.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the bitmap is null or empty.</exception>
    public void ApplyPremultiplyAlpha()
    {
        if (SkBitmap == null || SkBitmap.IsEmpty)
            throw new ArgumentException("Invalid bitmap.");

        Premultiplied = true;

        ClearTileCache();

        SkBitmapOriginal?.Dispose();
        SkBitmapOriginal = SkBitmap.Copy();

        SkBitmap = SkBitmap.PremultiplyAlpha();

        BuildTileCache();
    }

    /// <summary>
    /// Encodes the tilesheet bitmap to a byte array in the specified image format.
    /// </summary>
    /// <param name="format">The image format to encode to. Defaults to PNG.</param>
    /// <param name="quality">The encoding quality (0-100). Defaults to 100 (highest quality).</param>
    /// <returns>A byte array containing the encoded image data.</returns>
    /// <exception cref="ArgumentException">Thrown when the bitmap is null or empty.</exception>
    public byte[] ToByteArray(SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
    {
        if (SkBitmap == null || SkBitmap.IsEmpty)
            throw new ArgumentException("Invalid bitmap.");

        return SkBitmap.EncodeBitmapToBytes(format, quality);
    }

    /// <summary>
    /// Retrieves the SkiaSharp image for the tile at the specified region and coordinates.
    /// </summary>
    /// <param name="regionName">The name of the tilesheet region.</param>
    /// <param name="x">The zero-based tile column index within the region.</param>
    /// <param name="y">The zero-based tile row index within the region.</param>
    /// <returns>
    /// The <see cref="SKImage"/> for the specified tile, or <see langword="null"/> if the region or coordinates
    /// are out of bounds or the tile cache is not initialized.
    /// </returns>
    public SKImage? GetImage(string regionName, int x, int y)
    {
        var region = GetRegion(regionName);

        if (region == null)
            return null;

        return region.GetImage(x, y);
    }

    /// <summary>
    /// Retrieves the SkiaSharp bitmap for the tile at the specified region and coordinates.
    /// </summary>
    /// <param name="regionName">The name of the tilesheet region.</param>
    /// <param name="x">The zero-based tile column index within the region.</param>
    /// <param name="y">The zero-based tile row index within the region.</param>
    /// <returns>
    /// The <see cref="SKBitmap"/> for the specified tile, or <see langword="null"/> if the region or coordinates
    /// are out of bounds or the tile cache is not initialized.
    /// </returns>
    public SKBitmap? GetBitmap(string regionName, int x, int y)
    {
        var region = GetRegion(regionName);

        if (region == null)
            return null;

        return region.GetBitmap(x, y);
    }

    /// <summary>
    /// Retrieves all tile bitmaps from the tilesheet as a dictionary indexed by their region and coordinates.
    /// </summary>
    /// <returns>
    /// A dictionary where keys are (regionName, x, y) tuples representing tile coordinates and values are the
    /// corresponding <see cref="SKBitmap"/> instances.
    /// </returns>
    public Dictionary<(string regionName, int x, int y), SKBitmap> GetAllBitmaps()
    {
        var tiles = new Dictionary<(string regionName, int x, int y), SKBitmap>();

        foreach (var region in Regions)
        {
            var regionTiles = region.GetAllBitmaps();

            foreach (var tile in regionTiles)
            {
                tiles[(region.Name, tile.Key.x, tile.Key.y)] = tile.Value;
            }
        }

        return tiles;
    }

    /// <summary>
    /// Retrieves all tile images from the tilesheet as a dictionary indexed by their region and coordinates.
    /// </summary>
    /// <returns>
    /// A dictionary where keys are (regionName, x, y) tuples representing tile coordinates and values are the
    /// corresponding <see cref="SKImage"/> instances.
    /// </returns>
    public Dictionary<(string regionName, int x, int y), SKImage> GetAllImages()
    {
        var tiles = new Dictionary<(string regionName, int x, int y), SKImage>();

        foreach (var region in Regions)
        {
            var regionTiles = region.GetAllImages();

            foreach (var tile in regionTiles)
            {
                tiles[(region.Name, tile.Key.x, tile.Key.y)] = tile.Value;
            }
        }

        return tiles;
    }

    private void BuildTileCache()
    {
        foreach (var region in Regions)
        {
            region.BuildTileCache();
        }
    }

    private void ClearTileCache()
    {
        foreach (var region in Regions)
        {
            region.ClearTileCache();
        }
    }

    // Inside Tilesheet
    /// <summary>
    /// Returns a <see cref="Frame"/> representing the tile at the given
    /// region and sheet coordinates.
    /// </summary>
    /// <param name="regionName">The name of the tilesheet region.</param>
    /// <param name="x">Zero-based tile column index within the region.</param>
    /// <param name="y">Zero-based tile row index within the region.</param>
    public Frame this[string regionName, int x, int y] => new Frame(this, regionName, x, y);

    #endregion Methods

    // --- IDisposable pattern ---
    private bool _disposed;

    /// <summary>
    /// Releases all resources used by this tilesheet, including cached tiles, bitmaps,
    /// and registration in the <see cref="TilesheetRegistry"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            // unregister from registry
            TilesheetRegistry.Instance.Remove(_name, this, dispose: false);

            // clean up tile cache
            foreach (var region in Regions)
            {
                region.Dispose();
            }

            Regions.Clear();

            // dispose the main bitmaps
            SkBitmap?.Dispose();
            SkBitmapOriginal?.Dispose();

            try
            {
                Disposed?.Invoke();
            }
            catch (Exception ex)
            {
                Engine.Logger.LogError(ex, "Error during Tilesheet Disposed event handling.");
            }

            // break delegate references
            Disposed = null;
        }

        // no unmanaged resources to free
    }
}