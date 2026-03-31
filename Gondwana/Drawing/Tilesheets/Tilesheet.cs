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
    private readonly struct TilesheetSlice
    {
        public readonly SKBitmap Bitmap;
        public readonly SKImage Image;

        public TilesheetSlice(SKBitmap bmp, SKImage img)
        {
            Bitmap = bmp;
            Image = img;
        }
    }

    private TilesheetSlice?[,]? _tileCache;

    /// <summary>
    /// Occurs when this tilesheet is disposed.
    /// </summary>
    public event EventHandler<TilesheetDisposedEventArgs> Disposed;

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
        SkBitmap = bitmap;
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
        InitialOffsetX = baseSheet.InitialOffsetX;
        InitialOffsetY = baseSheet.InitialOffsetY;
        XPixelsBetweenTiles = baseSheet.XPixelsBetweenTiles;
        YPixelsBetweenTiles = baseSheet.YPixelsBetweenTiles;
        _tileSize = baseSheet._tileSize;
        OverhangPixels = baseSheet.OverhangPixels;
        ValueBag = new(baseSheet.ValueBag);

        _name = name;
        SkBitmap = SKBitmap.Decode(file);
        ImageFilePath = file;

        TilesheetRegistry.Instance.Register(this);
    }

    /// <summary>
    /// Gets the SkiaSharp bitmap containing the tilesheet image.
    /// This may be a modified version if alpha masking or premultiplication has been applied.
    /// </summary>
    [JsonIgnore]
    public SKBitmap SkBitmap { get; private set; }

    /// <summary>
    /// Gets the original SkiaSharp bitmap before any alpha masking or premultiplication was applied.
    /// Returns <see langword="null"/> if no modifications have been made.
    /// </summary>
    [JsonIgnore]
    public SKBitmap? SkBitmapOriginal { get; private set; } = null;

    [JsonProperty]
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

    [JsonProperty]
    private Size _tileSize;

    /// <summary>
    /// Gets or sets the size of each individual tile in the tilesheet.
    /// Setting this property rebuilds the internal tile cache.
    /// </summary>
    [JsonIgnore]
    public Size TileSize
    {
        get => _tileSize;
        set
        {
            _tileSize = value;
            BuildTileCache();
        }
    }

    /// <summary>
    /// Gets or sets the overhang dimensions (in pixels) that extend beyond each tile's base boundaries;
    /// i.e., how much of the tile should be considered the "overhang" portion when rendering.
    /// </summary>
    [JsonProperty]
    public Overhang OverhangPixels { get; set; } = Overhang.None;

    [JsonProperty]
    private int _initialOffsetX;

    /// <summary>
    /// Gets or sets the horizontal offset (in pixels) from the left edge of the tilesheet to the first tile.
    /// Setting this property rebuilds the internal tile cache.
    /// </summary>
    [JsonIgnore]
    public int InitialOffsetX
    {
        get => _initialOffsetX;
        set
        {
            _initialOffsetX = value;
            BuildTileCache();
        }
    }

    [JsonProperty]
    private int _initialOffsetY;

    /// <summary>
    /// Gets or sets the vertical offset (in pixels) from the top edge of the tilesheet to the first tile.
    /// Setting this property rebuilds the internal tile cache.
    /// </summary>
    [JsonIgnore]
    public int InitialOffsetY
    {
        get => _initialOffsetY;
        set
        {
            _initialOffsetY = value;
            BuildTileCache();
        }
    }

    [JsonProperty]
    private int _xPixelsBetweenTiles;

    /// <summary>
    /// Gets or sets the horizontal spacing (in pixels) between tiles in the tilesheet.
    /// Setting this property rebuilds the internal tile cache.
    /// </summary>
    [JsonIgnore]
    public int XPixelsBetweenTiles
    {
        get => _xPixelsBetweenTiles;
        set
        {
            _xPixelsBetweenTiles = value;
            BuildTileCache();
        }
    }

    [JsonProperty]
    private int _yPixelsBetweenTiles;

    /// <summary>
    /// Gets or sets the vertical spacing (in pixels) between tiles in the tilesheet.
    /// Setting this property rebuilds the internal tile cache.
    /// </summary>
    [JsonIgnore]
    public int YPixelsBetweenTiles
    {
        get => _yPixelsBetweenTiles;
        set
        {
            _yPixelsBetweenTiles = value;
            BuildTileCache();
        }
    }

    /// <summary>
    /// Gets or sets the value bag for storing arbitrary typed values associated with this tilesheet.
    /// </summary>
    [JsonProperty]
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

        MaskColor = maskColor;
        MaskTolerance = tolerance;
        Premultiplied = true;

        var targetColor = maskColor ?? SKColors.White;

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

        SkBitmapOriginal = SkBitmap.Copy();

        SkiaHelper.ApplyAlphaMask(SkBitmap, targetColor, tolerance);
        SkBitmap = SkiaHelper.PremultiplyAlpha(SkBitmap);

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

        SkBitmapOriginal = SkBitmap.Copy();
        SkBitmap = SkiaHelper.PremultiplyAlpha(SkBitmap);
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

        return SkiaHelper.EncodeBitmapToBytes(SkBitmap, format, quality);
    }

    private Rectangle GetTileBounds(int xTile, int yTile)
    {
        int x = xTile * (_tileSize.Width + XPixelsBetweenTiles) + InitialOffsetX;
        int y = yTile * (_tileSize.Height + YPixelsBetweenTiles) + InitialOffsetY;
        return new Rectangle(new Point(x, y), _tileSize);
    }

    private void BuildTileCache()
    {
        ClearCache();

        if (TileSize.Width <= 0 || TileSize.Height <= 0)
            return;

        int xTiles = (SkBitmap.Width - InitialOffsetX + XPixelsBetweenTiles) / (_tileSize.Width + XPixelsBetweenTiles);
        int yTiles = (SkBitmap.Height - InitialOffsetY + YPixelsBetweenTiles) / (_tileSize.Height + YPixelsBetweenTiles);

        _tileCache = new TilesheetSlice?[xTiles, yTiles];

        for (int y = 0; y < yTiles; y++)
        {
            for (int x = 0; x < xTiles; x++)
            {
                var srcRect = GetTileBounds(x, y);
                if (!SkBitmap.Info.Rect.Contains(srcRect.ToSKRectI()))
                    continue;

                var srcInfo = SkBitmap.Info;

                // IMPORTANT: preserve alpha + color type from the masked tilesheet bitmap
                var sliceInfo = new SKImageInfo(
                    _tileSize.Width,
                    _tileSize.Height,
                    srcInfo.ColorType,
                    srcInfo.AlphaType
                );

                var bmp = new SKBitmap(sliceInfo);

                // Optional but nice: ensure any untouched pixels are transparent
                bmp.Erase(SKColors.Transparent);

                if (SkBitmap.ExtractSubset(bmp, srcRect.ToSKRectI()))
                {
                    var img = SKImage.FromBitmap(bmp);
                    _tileCache[x, y] = new TilesheetSlice(bmp, img);
                }
                else
                {
                    bmp.Dispose();
                }
            }
        }
    }

    private void ClearCache()
    {
        if (_tileCache == null)
            return;

        for (int y = 0; y < _tileCache.GetLength(1); y++)
        {
            for (int x = 0; x < _tileCache.GetLength(0); x++)
            {
                _tileCache[x, y]?.Bitmap.Dispose();
                _tileCache[x, y]?.Image.Dispose();
                _tileCache[x, y] = null;
            }
        }

        _tileCache = null;
    }

    /// <summary>
    /// Retrieves the SkiaSharp image for the tile at the specified coordinates.
    /// </summary>
    /// <param name="x">The zero-based tile column index.</param>
    /// <param name="y">The zero-based tile row index.</param>
    /// <returns>
    /// The <see cref="SKImage"/> for the specified tile, or <see langword="null"/> if the coordinates
    /// are out of bounds or the tile cache is not initialized.
    /// </returns>
    public SKImage? GetImage(int x, int y)
    {
        if (_tileCache == null)
            BuildTileCache();

        if (_tileCache == null)
            return null;

        if ((uint)x >= (uint)_tileCache.GetLength(0) || (uint)y >= (uint)_tileCache.GetLength(1))
            return null;

        return _tileCache?[x, y]?.Image;
    }

    /// <summary>
    /// Retrieves the SkiaSharp bitmap for the tile at the specified coordinates.
    /// </summary>
    /// <param name="x">The zero-based tile column index.</param>
    /// <param name="y">The zero-based tile row index.</param>
    /// <returns>
    /// The <see cref="SKBitmap"/> for the specified tile, or <see langword="null"/> if the coordinates
    /// are out of bounds or the tile cache is not initialized.
    /// </returns>
    public SKBitmap? GetBitmap(int x, int y)
    {
        if (_tileCache == null)
            BuildTileCache();

        if (_tileCache == null)
            return null;

        if ((uint)x >= (uint)_tileCache.GetLength(0) || (uint)y >= (uint)_tileCache.GetLength(1))
            return null;

        return _tileCache?[x, y]?.Bitmap;
    }

    /// <summary>
    /// Retrieves all tile bitmaps from the tilesheet as a dictionary indexed by their coordinates.
    /// </summary>
    /// <returns>
    /// A dictionary where keys are (x, y) tuples representing tile coordinates and values are the
    /// corresponding <see cref="SKBitmap"/> instances.
    /// </returns>
    public Dictionary<(int x, int y), SKBitmap> GetAllBitmaps()
    {
        if (_tileCache == null)
            BuildTileCache();

        var tiles = new Dictionary<(int x, int y), SKBitmap>();
        if (_tileCache == null)
            return tiles;

        int xTiles = _tileCache.GetLength(0);
        int yTiles = _tileCache.GetLength(1);

        for (int y = 0; y < yTiles; y++)
        {
            for (int x = 0; x < xTiles; x++)
            {
                var slice = _tileCache[x, y];
                if (slice.HasValue)
                    tiles[(x, y)] = slice.Value.Bitmap;
            }
        }

        return tiles;
    }

    /// <summary>
    /// Retrieves all tile images from the tilesheet as a dictionary indexed by their coordinates.
    /// </summary>
    /// <returns>
    /// A dictionary where keys are (x, y) tuples representing tile coordinates and values are the
    /// corresponding <see cref="SKImage"/> instances.
    /// </returns>
    public Dictionary<(int x, int y), SKImage> GetAllImages()
    {
        if (_tileCache == null)
            BuildTileCache();

        var tiles = new Dictionary<(int x, int y), SKImage>();
        if (_tileCache == null)
            return tiles;

        int xTiles = _tileCache.GetLength(0);
        int yTiles = _tileCache.GetLength(1);

        for (int y = 0; y < yTiles; y++)
        {
            for (int x = 0; x < xTiles; x++)
            {
                var slice = _tileCache[x, y];
                if (slice.HasValue)
                    tiles[(x, y)] = slice.Value.Image;
            }
        }

        return tiles;
    }

    // Inside Tilesheet
    /// <summary>
    /// Returns a <see cref="Frame"/> representing the tile at the given
    /// sheet coordinates.
    /// </summary>
    /// <param name="x">Zero-based tile column index.</param>
    /// <param name="y">Zero-based tile row index.</param>
    public Frame this[int x, int y] => new Frame(this, x, y);

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
            if (_tileCache != null)
            {
                for (int x = 0; x < _tileCache.GetLength(0); x++)
                {
                    for (int y = 0; y < _tileCache.GetLength(1); y++)
                    {
                        _tileCache[x, y]?.Bitmap?.Dispose();
                        _tileCache[x, y]?.Image?.Dispose();
                    }
                }
                _tileCache = null;
            }

            // dispose the main bitmaps
            SkBitmap?.Dispose();
            SkBitmapOriginal?.Dispose();

            try
            {
                Disposed?.Invoke(this, new TilesheetDisposedEventArgs(this));
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