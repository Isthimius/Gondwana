using System.Drawing;
using Newtonsoft.Json;
using SkiaSharp;
using Gondwana.Drawing;
using Gondwana.SkiaSharp;

namespace Gondwana.Drawing.Tilesheets;

/// <summary>
/// Represents a rectangular region within a tilesheet that contains a grid of tiles.
/// </summary>
public sealed class TilesheetRegion : IDisposable
{
    /// <summary>
    /// The default name assigned to tilesheet regions when no name is specified.
    /// </summary>
    public static readonly string DefaultRegionName = "default";

    private TilesheetRegionSlice?[,]? _tileCache;
    private bool _disposed;

    #region ctors

    [JsonConstructor]
    private TilesheetRegion() { }

    internal TilesheetRegion(
        Tilesheet tilesheet,
        string name,
        Rectangle area,
        Size tileSize,
        Spacing tilePadding,
        Spacing regionMargin,
        Spacing overhangPixels)
    {
        Tilesheet = tilesheet ?? throw new ArgumentNullException(nameof(tilesheet));

        Name = string.IsNullOrWhiteSpace(name)
            ? DefaultRegionName
            : name;

        // Assign backing fields directly so we do not rebuild the cache
        // repeatedly during construction.
        _area = area;
        _tileSize = tileSize;
        _tilePadding = tilePadding;
        _regionMargin = regionMargin;

        Overhang = overhangPixels;

        BuildTileCache();
    }

    #endregion ctors

    #region serialized fields

    [JsonProperty("area")]
    private Rectangle _area;

    [JsonProperty("tileSize")]
    private Size _tileSize;

    [JsonProperty("tilePadding")]
    private Spacing _tilePadding = Spacing.None;

    [JsonProperty("regionMargin")]
    private Spacing _regionMargin = Spacing.None;

    #endregion serialized fields

    #region properties

    /// <summary>
    /// Gets the tilesheet that owns this region.
    /// </summary>
    [JsonIgnore]
    public Tilesheet Tilesheet { get; private set; } = null!;

    /// <summary>
    /// Gets the name of this tilesheet region.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; private set; } = DefaultRegionName;

    /// <summary>
    /// Gets or sets the rectangular area that this region occupies within the tilesheet.
    /// Setting this property rebuilds the internal tile cache.
    /// </summary>
    [JsonIgnore]
    public Rectangle Area
    {
        get => _area;
        set
        {
            _area = value;
            BuildTileCache();
        }
    }

    /// <summary>
    /// Gets or sets the size of each individual tile in this region.
    /// Setting this property rebuilds the internal tile cache.
    /// <para />
    /// The tile size defines the source pixel dimensions of each tile's primary area, excluding any padding
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
    /// Gets or sets the spacing (padding) around each tile within this region.
    /// Setting this property rebuilds the internal tile cache.
    /// </summary>
    [JsonIgnore]
    public Spacing TilePadding
    {
        get => _tilePadding;
        set
        {
            _tilePadding = value;
            BuildTileCache();
        }
    }

    /// <summary>
    /// Gets or sets the margin spacing around the entire region.
    /// Setting this property rebuilds the internal tile cache.
    /// </summary>
    [JsonIgnore]
    public Spacing RegionMargin
    {
        get => _regionMargin;
        set
        {
            _regionMargin = value;
            BuildTileCache();
        }
    }

    /// <summary>
    /// Represents the overhang dimensions in pixels that extend beyond a tile's primary area.
    /// Overhang values define how much a tile's visual representation exceeds its logical boundaries
    /// in each direction (left, top, right, and bottom).
    /// <para />
    /// This property only affects how the tile is rendered; it does not affect how the tile is sliced
    /// and cached.
    /// </summary>
    [JsonProperty("overhang")]
    public Spacing Overhang { get; set; } = Spacing.None;

    /// <summary>
    /// Gets the number of columns (horizontal tiles) in this region.
    /// </summary>
    [JsonIgnore]
    public int Columns => _tileCache?.GetLength(0) ?? 0;

    /// <summary>
    /// Gets the number of rows (vertical tiles) in this region.
    /// </summary>
    [JsonIgnore]
    public int Rows => _tileCache?.GetLength(1) ?? 0;

    /// <summary>
    /// Gets the total width of a single tile including its padding.
    /// </summary>
    [JsonIgnore]
    public int TileWidthIncludingPadding => _tilePadding.Left + _tileSize.Width + _tilePadding.Right;

    /// <summary>
    /// Gets the total height of a single tile including its padding.
    /// </summary>
    [JsonIgnore]
    public int TileHeightIncludingPadding => _tilePadding.Top + _tileSize.Height + _tilePadding.Bottom;

    #endregion properties

    #region public methods

    /// <summary>
    /// Gets the image for the tile at the specified grid coordinates.
    /// </summary>
    /// <param name="x">The column index of the tile.</param>
    /// <param name="y">The row index of the tile.</param>
    /// <returns>The SKImage for the tile, or null if the coordinates are out of bounds or the tile cache is invalid.</returns>
    public SKImage? GetImage(int x, int y)
    {
        ThrowIfDisposed();

        if (_tileCache == null)
            BuildTileCache();

        if (_tileCache == null)
            return null;

        if ((uint)x >= (uint)_tileCache.GetLength(0) ||
            (uint)y >= (uint)_tileCache.GetLength(1))
            return null;

        return _tileCache[x, y]?.Image;
    }

    /// <summary>
    /// Gets the bitmap for the tile at the specified grid coordinates.
    /// </summary>
    /// <param name="x">The column index of the tile.</param>
    /// <param name="y">The row index of the tile.</param>
    /// <returns>The SKBitmap for the tile, or null if the coordinates are out of bounds or the tile cache is invalid.</returns>
    public SKBitmap? GetBitmap(int x, int y)
    {
        ThrowIfDisposed();

        if (_tileCache == null)
            BuildTileCache();

        if (_tileCache == null)
            return null;

        if ((uint)x >= (uint)_tileCache.GetLength(0) ||
            (uint)y >= (uint)_tileCache.GetLength(1))
            return null;

        return _tileCache[x, y]?.Bitmap;
    }

    /// <summary>
    /// Gets all bitmaps in this region as a dictionary keyed by their grid coordinates.
    /// </summary>
    /// <returns>A dictionary mapping (x, y) coordinates to their corresponding SKBitmap instances.</returns>
    public Dictionary<(int x, int y), SKBitmap> GetAllBitmaps()
    {
        ThrowIfDisposed();

        if (_tileCache == null)
            BuildTileCache();

        var bitmaps = new Dictionary<(int x, int y), SKBitmap>();

        if (_tileCache == null)
            return bitmaps;

        for (int y = 0; y < _tileCache.GetLength(1); y++)
        {
            for (int x = 0; x < _tileCache.GetLength(0); x++)
            {
                var slice = _tileCache[x, y];

                if (slice.HasValue)
                    bitmaps[(x, y)] = slice.Value.Bitmap;
            }
        }

        return bitmaps;
    }

    /// <summary>
    /// Gets all images in this region as a dictionary keyed by their grid coordinates.
    /// </summary>
    /// <returns>A dictionary mapping (x, y) coordinates to their corresponding SKImage instances.</returns>
    public Dictionary<(int x, int y), SKImage> GetAllImages()
    {
        ThrowIfDisposed();

        if (_tileCache == null)
            BuildTileCache();

        var images = new Dictionary<(int x, int y), SKImage>();

        if (_tileCache == null)
            return images;

        for (int y = 0; y < _tileCache.GetLength(1); y++)
        {
            for (int x = 0; x < _tileCache.GetLength(0); x++)
            {
                var slice = _tileCache[x, y];

                if (slice.HasValue)
                    images[(x, y)] = slice.Value.Image;
            }
        }

        return images;
    }

    #endregion public methods

    #region internal methods

    internal void BuildTileCache()
    {
        ThrowIfDisposed();
        ClearTileCache();

        if (Tilesheet == null)
            return;

        if (Tilesheet.SkBitmap == null || Tilesheet.SkBitmap.IsEmpty)
            return;

        if (_tileSize.Width <= 0 || _tileSize.Height <= 0)
            return;

        if (_area.Width <= 0 || _area.Height <= 0)
            return;

        if (_tilePadding.Left < 0 || _tilePadding.Top < 0 || _tilePadding.Right < 0 || _tilePadding.Bottom < 0)
            throw new InvalidOperationException("Tilesheet region tile padding cannot be negative.");

        if (_regionMargin.Left < 0 || _regionMargin.Top < 0 || _regionMargin.Right < 0 || _regionMargin.Bottom < 0)
            throw new InvalidOperationException("Tilesheet region margin cannot be negative.");

        if (TileWidthIncludingPadding <= 0 || TileHeightIncludingPadding <= 0)
            return;

        int xTiles = (_area.Width - _regionMargin.Left - _regionMargin.Right) / TileWidthIncludingPadding;
        int yTiles = (_area.Height - _regionMargin.Top - _regionMargin.Bottom) / TileHeightIncludingPadding;

        if (xTiles <= 0 || yTiles <= 0)
            return;

        _tileCache = new TilesheetRegionSlice?[xTiles, yTiles];

        var regionArea = Area;
        var bitmapBounds = Tilesheet.SkBitmap.Info.Rect;

        for (int y = 0; y < yTiles; y++)
        {
            for (int x = 0; x < xTiles; x++)
            {
                var srcRect = GetTileBounds(x, y);

                // Prevent this region from bleeding into another differently-sized row/section.
                if (!regionArea.Contains(srcRect))
                    continue;

                // Prevent invalid reads outside the source image.
                if (!bitmapBounds.Contains(srcRect.ToSKRectI()))
                    continue;

                var slice = CreateSlice(srcRect);

                if (slice.HasValue)
                    _tileCache[x, y] = slice.Value;
            }
        }
    }

    internal void ClearTileCache()
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

    #endregion internal methods

    #region private methods

    private Rectangle GetTileBounds(int xTile, int yTile)
    {
        int x = _area.X + _regionMargin.Left + (xTile * TileWidthIncludingPadding);
        int y = _area.Y + _regionMargin.Top + (yTile * TileHeightIncludingPadding);

        return new Rectangle(x + _tilePadding.Left, y + _tilePadding.Top, _tileSize.Width, _tileSize.Height);
    }

    private TilesheetRegionSlice? CreateSlice(Rectangle srcRect)
    {
        var srcInfo = Tilesheet.SkBitmap.Info;

        var sliceInfo = new SKImageInfo(
            srcRect.Width,
            srcRect.Height,
            srcInfo.ColorType,
            srcInfo.AlphaType);

        var bmp = new SKBitmap(sliceInfo);
        bmp.Erase(SKColors.Transparent);

        if (!Tilesheet.SkBitmap.ExtractSubset(bmp, srcRect.ToSKRectI()))
        {
            bmp.Dispose();
            return null;
        }

        var img = SKImage.FromBitmap(bmp);

        return new TilesheetRegionSlice(bmp, img);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TilesheetRegion));
    }

    #endregion private methods

    #region IDisposable

    /// <summary>
    /// Releases all resources used by this TilesheetRegion, including the tile cache.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        ClearTileCache();
    }

    #endregion IDisposable
}