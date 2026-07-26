using System.Drawing;
using Gondwana.Physics.Collisions;
using Gondwana.SkiaSharp;
using SkiaSharp;

namespace Gondwana.Drawing.Tilesheets;

/// <summary>
/// Represents a rectangular region within a tilesheet that contains a grid of frames.
/// </summary>
public sealed class TilesheetRegion : IDisposable
{
    /// <summary>
    /// The default name assigned to tilesheet regions when no name is specified.
    /// </summary>
    public static readonly string DefaultRegionName = "default";

    private TilesheetRegionSlice?[,]? _tileCache;
    private readonly Dictionary<(int x, int y), CollisionAdjust> _frameCollisionAdjustments = [];
    private CollisionAdjust _collisionAdjust = Gondwana.Physics.Collisions.CollisionAdjust.None;
    private bool _disposed;

    private TilesheetRegion() { }

    internal TilesheetRegion(
        Tilesheet tilesheet,
        string name,
        Rectangle area,
        Size tileSize,
        Spacing tilePadding,
        Spacing regionMargin,
        Spacing overhangPixels,
        CollisionAdjust collisionAdjust)
    {
        Tilesheet = tilesheet ?? throw new ArgumentNullException(nameof(tilesheet));
        Name = string.IsNullOrWhiteSpace(name) ? DefaultRegionName : name;

        // Assign backing fields directly so construction performs one cache build.
        _area = area;
        _tileSize = tileSize;
        _tilePadding = tilePadding;
        _regionMargin = regionMargin;
        Overhang = overhangPixels;
        _collisionAdjust = collisionAdjust;

        BuildTileCache();
    }

    private Rectangle _area;
    private Size _tileSize;
    private Spacing _tilePadding = Spacing.None;
    private Spacing _regionMargin = Spacing.None;

    /// <summary>
    /// Gets the tilesheet that owns this region.
    /// </summary>
    public Tilesheet Tilesheet { get; private set; } = null!;

    /// <summary>
    /// Gets the region name.
    /// </summary>
    public string Name { get; private set; } = DefaultRegionName;

    public Rectangle Area
    {
        get => _area;
        set
        {
            _area = value;
            BuildTileCache();
        }
    }

    public Size TileSize
    {
        get => _tileSize;
        set
        {
            _tileSize = value;
            BuildTileCache();
        }
    }

    public Spacing TilePadding
    {
        get => _tilePadding;
        set
        {
            _tilePadding = value;
            BuildTileCache();
        }
    }

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
    /// Gets or sets visual overhang. This affects rendering, not slicing.
    /// </summary>
    public Spacing Overhang { get; set; } = Spacing.None;

    /// <summary>
    /// Gets or sets the region-level collision adjustment. Assigning this value
    /// overwrites every frame adjustment in the region and updates cached metadata.
    /// </summary>
    public CollisionAdjust CollisionAdjust
    {
        get => _collisionAdjust;
        set
        {
            ThrowIfDisposed();
            _collisionAdjust = value;
            _frameCollisionAdjustments.Clear();
            ApplyCollisionAdjustToCache(value);
        }
    }

    /// <summary>
    /// Gets the region-default frame-local collision rectangle.
    /// </summary>
    public Rectangle CollisionArea =>
        _collisionAdjust.ApplyTo(new Rectangle(Point.Empty, _tileSize));

    public int Columns => _tileCache?.GetLength(0) ?? 0;
    public int Rows => _tileCache?.GetLength(1) ?? 0;
    public int TileWidthIncludingPadding => _tilePadding.Left + _tileSize.Width + _tilePadding.Right;
    public int TileHeightIncludingPadding => _tilePadding.Top + _tileSize.Height + _tilePadding.Bottom;

    public SKImage? GetImage(int x, int y)
    {
        ThrowIfDisposed();

        if (_tileCache == null)
            BuildTileCache();

        if (_tileCache == null ||
            (uint)x >= (uint)_tileCache.GetLength(0) ||
            (uint)y >= (uint)_tileCache.GetLength(1))
        {
            return null;
        }

        return _tileCache[x, y]?.Image;
    }

    public SKBitmap? GetBitmap(int x, int y)
    {
        ThrowIfDisposed();

        if (_tileCache == null)
            BuildTileCache();

        if (_tileCache == null ||
            (uint)x >= (uint)_tileCache.GetLength(0) ||
            (uint)y >= (uint)_tileCache.GetLength(1))
        {
            return null;
        }

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
                if (_tileCache[x, y] is { } slice)
                    bitmaps[(x, y)] = slice.Bitmap;
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
                if (_tileCache[x, y] is { } slice)
                    images[(x, y)] = slice.Image;
            }
        }

        return images;
    }

    /// <summary>
    /// Gets the effective collision adjustment for one frame.
    /// </summary>
    public CollisionAdjust GetFrameCollisionAdjust(int x, int y)
    {
        ThrowIfDisposed();

        if (!IsFrameCoordinateValid(x, y))
            return _collisionAdjust;

        if (_tileCache is not null && _tileCache[x, y] is { } slice)
            return slice.CollisionAdjust;

        return GetStoredFrameCollisionAdjust(x, y);
    }

    /// <summary>
    /// Sets the collision adjustment for one frame and updates its cache entry.
    /// </summary>
    public void SetFrameCollisionAdjust(int x, int y, CollisionAdjust collisionAdjust)
    {
        ThrowIfDisposed();

        if (_tileCache is null)
            BuildTileCache();

        if (!IsFrameCoordinateValid(x, y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Frame coordinates ({x}, {y}) are outside region '{Name}'.");
        }

        var key = (x, y);
        if (collisionAdjust == _collisionAdjust)
            _frameCollisionAdjustments.Remove(key);
        else
            _frameCollisionAdjustments[key] = collisionAdjust;

        if (_tileCache![x, y] is { } slice)
            _tileCache[x, y] = slice.WithCollisionAdjust(collisionAdjust);
    }

    /// <summary>
    /// Gets the frame-local collision rectangle for one frame.
    /// </summary>
    public Rectangle GetFrameCollisionArea(int x, int y) =>
        GetFrameCollisionAdjust(x, y)
            .ApplyTo(new Rectangle(Point.Empty, _tileSize));

    /// <summary>
    /// Builds the internal image/cache slices while preserving frame collision overrides.
    /// </summary>
    internal void BuildTileCache()
    {
        ThrowIfDisposed();
        ClearTileCache();

        if (Tilesheet == null ||
            Tilesheet.SkBitmap == null ||
            Tilesheet.SkBitmap.IsEmpty ||
            _tileSize.Width <= 0 ||
            _tileSize.Height <= 0 ||
            _area.Width <= 0 ||
            _area.Height <= 0)
        {
            return;
        }

        if (_tilePadding.Left < 0 || _tilePadding.Top < 0 ||
            _tilePadding.Right < 0 || _tilePadding.Bottom < 0)
        {
            throw new InvalidOperationException("Tilesheet region tile padding cannot be negative.");
        }

        if (_regionMargin.Left < 0 || _regionMargin.Top < 0 ||
            _regionMargin.Right < 0 || _regionMargin.Bottom < 0)
        {
            throw new InvalidOperationException("Tilesheet region margin cannot be negative.");
        }

        if (TileWidthIncludingPadding <= 0 || TileHeightIncludingPadding <= 0)
            return;

        int xTiles = (_area.Width - _regionMargin.Left - _regionMargin.Right) /
            TileWidthIncludingPadding;
        int yTiles = (_area.Height - _regionMargin.Top - _regionMargin.Bottom) /
            TileHeightIncludingPadding;

        if (xTiles <= 0 || yTiles <= 0)
            return;

        PruneFrameCollisionAdjustments(xTiles, yTiles);
        _tileCache = new TilesheetRegionSlice?[xTiles, yTiles];

        var regionArea = Area;
        var bitmapBounds = Tilesheet.SkBitmap.Info.Rect;

        for (int y = 0; y < yTiles; y++)
        {
            for (int x = 0; x < xTiles; x++)
            {
                var srcRect = GetTileBounds(x, y);

                // Prevent this region from bleeding into another region or outside the image.
                if (!regionArea.Contains(srcRect) ||
                    !bitmapBounds.Contains(srcRect.ToSKRectI()))
                {
                    continue;
                }

                var slice = CreateSlice(
                    srcRect,
                    GetStoredFrameCollisionAdjust(x, y));

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

    private Rectangle GetTileBounds(int xTile, int yTile)
    {
        int x = _area.X + _regionMargin.Left + (xTile * TileWidthIncludingPadding);
        int y = _area.Y + _regionMargin.Top + (yTile * TileHeightIncludingPadding);

        return new Rectangle(
            x + _tilePadding.Left,
            y + _tilePadding.Top,
            _tileSize.Width,
            _tileSize.Height);
    }

    private TilesheetRegionSlice? CreateSlice(
        Rectangle srcRect,
        CollisionAdjust collisionAdjust)
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
        return new TilesheetRegionSlice(bmp, img, collisionAdjust);
    }

    private CollisionAdjust GetStoredFrameCollisionAdjust(int x, int y) =>
        _frameCollisionAdjustments.TryGetValue((x, y), out var collisionAdjust)
            ? collisionAdjust
            : _collisionAdjust;

    private bool IsFrameCoordinateValid(int x, int y) =>
        _tileCache is not null &&
        (uint)x < (uint)_tileCache.GetLength(0) &&
        (uint)y < (uint)_tileCache.GetLength(1);

    private void ApplyCollisionAdjustToCache(CollisionAdjust collisionAdjust)
    {
        if (_tileCache is null)
            return;

        for (int y = 0; y < _tileCache.GetLength(1); y++)
        {
            for (int x = 0; x < _tileCache.GetLength(0); x++)
            {
                if (_tileCache[x, y] is { } slice)
                    _tileCache[x, y] = slice.WithCollisionAdjust(collisionAdjust);
            }
        }
    }

    private void PruneFrameCollisionAdjustments(int columns, int rows)
    {
        foreach (var key in _frameCollisionAdjustments.Keys.ToArray())
        {
            if ((uint)key.x >= (uint)columns || (uint)key.y >= (uint)rows)
                _frameCollisionAdjustments.Remove(key);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TilesheetRegion));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ClearTileCache();
        _frameCollisionAdjustments.Clear();
    }
}
