using System.Drawing;
using Newtonsoft.Json;
using SkiaSharp;
using Gondwana.Drawing;
using Gondwana.SkiaSharp;

namespace Gondwana.Drawing.Tilesheets;

public sealed class TilesheetRegion : IDisposable
{
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
        Size spacing,
        Size tileSize,
        Spacing overhangPixels)
    {
        Tilesheet = tilesheet ?? throw new ArgumentNullException(nameof(tilesheet));

        Name = string.IsNullOrWhiteSpace(name)
            ? DefaultRegionName
            : name;

        // Assign backing fields directly so we do not rebuild the cache
        // repeatedly during construction.
        _area = area;

        _spacingX = spacing.Width;
        _spacingY = spacing.Height;

        _tileSize = tileSize;

        Overhang = overhangPixels;

        BuildTileCache();
    }

    #endregion ctors

    #region serialized fields

    [JsonProperty("area")]
    private Rectangle _area;

    [JsonProperty("tileSize")]
    private Size _tileSize;

    [JsonProperty("spacingX")]
    private int _spacingX;

    [JsonProperty("spacingY")]
    private int _spacingY;

    #endregion serialized fields

    #region properties

    [JsonIgnore]
    public Tilesheet Tilesheet { get; private set; } = null!;

    [JsonProperty("name")]
    public string Name { get; private set; } = DefaultRegionName;

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

    [JsonIgnore]
    public Size Spacing
    {
        get => new(_spacingX, _spacingY);
        set
        {
            _spacingX = value.Width;
            _spacingY = value.Height;

            BuildTileCache();
        }
    }

    /// <summary>
    /// Gets or sets the size of each individual tile in this region.
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
    /// Represents the overhang dimensions in pixels that extend beyond a tile's primary area.
    /// Overhang values define how much a tile's visual representation exceeds its logical boundaries
    /// in each direction (left, top, right, and bottom).
    /// <para />
    /// This property only affects how the tile is rendered; it does not affect how the tile is sliced
    /// and cached.
    /// </summary>
    [JsonProperty("overhang")]
    public Spacing Overhang { get; set; } = Drawing.Spacing.None;

    [JsonIgnore]
    public int Columns => _tileCache?.GetLength(0) ?? 0;

    [JsonIgnore]
    public int Rows => _tileCache?.GetLength(1) ?? 0;

    #endregion properties

    #region public methods

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

        if (_spacingX < 0 || _spacingY < 0)
            throw new InvalidOperationException("Tilesheet region spacing cannot be negative.");

        int strideX = _tileSize.Width + _spacingX;
        int strideY = _tileSize.Height + _spacingY;

        if (strideX <= 0 || strideY <= 0)
            return;

        int xTiles = (_area.Width + _spacingX) / strideX;
        int yTiles = (_area.Height + _spacingY) / strideY;

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
        int x = _area.X + xTile * (_tileSize.Width + _spacingX);
        int y = _area.Y + yTile * (_tileSize.Height + _spacingY);

        return new Rectangle(x, y, _tileSize.Width, _tileSize.Height);
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

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        ClearTileCache();
    }

    #endregion IDisposable
}