using Gondwana.SkiaSharp;
using Newtonsoft.Json;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Drawing.Tilesheets;

public sealed class TilesheetRegion : IDisposable
{
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
        Overhang overhangPixels)
    {
        Tilesheet = tilesheet ?? throw new ArgumentNullException(nameof(tilesheet));

        Name = string.IsNullOrWhiteSpace(name)
            ? "default"
            : name;

        // Assign backing fields directly so we do not rebuild the cache
        // repeatedly during construction.
        _x = area.X;
        _y = area.Y;
        _width = area.Width;
        _height = area.Height;

        _spacingX = spacing.Width;
        _spacingY = spacing.Height;

        _tileWidth = tileSize.Width;
        _tileHeight = tileSize.Height;

        OverhangPixels = overhangPixels;

        BuildTileCache();
    }

    #endregion ctors

    #region serialized fields

    [JsonProperty("x")]
    private int _x;

    [JsonProperty("y")]
    private int _y;

    [JsonProperty("width")]
    private int _width;

    [JsonProperty("height")]
    private int _height;

    [JsonProperty("spacingX")]
    private int _spacingX;

    [JsonProperty("spacingY")]
    private int _spacingY;

    [JsonProperty("tileWidth")]
    private int _tileWidth;

    [JsonProperty("tileHeight")]
    private int _tileHeight;

    #endregion serialized fields

    #region properties

    [JsonIgnore]
    public Tilesheet Tilesheet { get; private set; } = null!;

    [JsonProperty("name")]
    public string Name { get; private set; } = "default";

    [JsonIgnore]
    public Rectangle Area
    {
        get => new(_x, _y, _width, _height);
        set
        {
            _x = value.X;
            _y = value.Y;
            _width = value.Width;
            _height = value.Height;

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
        get => new(_tileWidth, _tileHeight);
        set
        {
            _tileWidth = value.Width;
            _tileHeight = value.Height;

            BuildTileCache();
        }
    }

    /// <summary>
    /// Gets or sets the overhang dimensions, in pixels, that extend beyond each tile's base boundaries.
    /// </summary>
    [JsonProperty("overhangPixels")]
    public Overhang OverhangPixels { get; set; } = Overhang.None;

    [JsonIgnore]
    public int Columns => _tileCache?.GetLength(0) ?? 0;

    [JsonIgnore]
    public int Rows => _tileCache?.GetLength(1) ?? 0;

    #endregion properties

    #region public methods

    public SKImage? GetImage(int x, int y)
    {
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
        ClearTileCache();

        if (Tilesheet == null)
            return;

        if (Tilesheet.SkBitmap == null || Tilesheet.SkBitmap.IsEmpty)
            return;

        if (_tileWidth <= 0 || _tileHeight <= 0)
            return;

        if (_width <= 0 || _height <= 0)
            return;

        if (_spacingX < 0 || _spacingY < 0)
            throw new InvalidOperationException("Tilesheet region spacing cannot be negative.");

        int strideX = _tileWidth + _spacingX;
        int strideY = _tileHeight + _spacingY;

        if (strideX <= 0 || strideY <= 0)
            return;

        int xTiles = (_width + _spacingX) / strideX;
        int yTiles = (_height + _spacingY) / strideY;

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
        int x = _x + xTile * (_tileWidth + _spacingX);
        int y = _y + yTile * (_tileHeight + _spacingY);

        return new Rectangle(x, y, _tileWidth, _tileHeight);
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