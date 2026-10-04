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
    private readonly Dictionary<(int x, int y), TileCollisionType> _frameCollisionTypes = [];
    private CollisionAdjust _collisionAdjust = Gondwana.Physics.Collisions.CollisionAdjust.None;
    private TileCollisionType _collisionType = TileCollisionType.None;
    private bool _disposed;

    private TilesheetRegion() { }

    /// <summary>
    /// Initializes a new instance of <see cref="TilesheetRegion"/>.
    /// </summary>
    /// <param name="tilesheet">The tilesheet that owns the region.</param>
    /// <param name="name">The region name.</param>
    /// <param name="area">The source-image area occupied by the region.</param>
    /// <param name="tileSize">The unpadded size of each frame.</param>
    /// <param name="tilePadding">The padding around each frame.</param>
    /// <param name="regionMargin">The margins inside the region area.</param>
    /// <param name="overhangPixels">The visual overhang applied when frames are rendered.</param>
    /// <param name="collisionAdjust">The default collision adjustment inherited by frames.</param>
    /// <param name="collisionType">The default collision type inherited by frames.</param>
    internal TilesheetRegion(
        Tilesheet tilesheet,
        string name,
        Rectangle area,
        Size tileSize,
        Spacing tilePadding,
        Spacing regionMargin,
        Spacing overhangPixels,
        CollisionAdjust collisionAdjust,
        TileCollisionType collisionType)
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
        _collisionType = collisionType;

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

    /// <summary>
    /// Gets or sets the area.
    /// </summary>
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
    /// Gets or sets the tile size.
    /// </summary>
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
    /// Gets or sets the tile padding.
    /// </summary>
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
    /// Gets or sets the region margin.
    /// </summary>
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
    /// Gets or sets the collision adjustment inherited by frames that do not have
    /// an explicit frame-level override.
    /// </summary>
    public CollisionAdjust CollisionAdjust
    {
        get => _collisionAdjust;
        set
        {
            ThrowIfDisposed();
            _collisionAdjust = value;
            ApplyDefaultCollisionAdjustToCache();
        }
    }

    /// <summary>
    /// Gets the region-default frame-local collision rectangle.
    /// </summary>
    public Rectangle CollisionArea =>
        _collisionAdjust.ApplyTo(new Rectangle(Point.Empty, _tileSize));

    /// <summary>
    /// Gets or sets the collision type inherited by frames that do not have an
    /// explicit frame-level override.
    /// </summary>
    public TileCollisionType CollisionType
    {
        get => _collisionType;
        set
        {
            ThrowIfDisposed();
            _collisionType = value;
        }
    }

    /// <summary>
    /// Gets the columns.
    /// </summary>
    public int Columns => _tileCache?.GetLength(0) ?? 0;
    /// <summary>
    /// Gets the rows.
    /// </summary>
    public int Rows => _tileCache?.GetLength(1) ?? 0;
    /// <summary>
    /// Gets the tile width including padding.
    /// </summary>
    public int TileWidthIncludingPadding => _tilePadding.Left + _tileSize.Width + _tilePadding.Right;
    /// <summary>
    /// Gets the tile height including padding.
    /// </summary>
    public int TileHeightIncludingPadding => _tilePadding.Top + _tileSize.Height + _tilePadding.Bottom;

    /// <summary>
    /// Gets image.
    /// </summary>
    /// <param name="x">The horizontal tile coordinate.</param>
    /// <param name="y">The vertical tile coordinate.</param>
    /// <returns>The requested value, or <see langword="null"/> when it is unavailable.</returns>
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

    /// <summary>
    /// Gets bitmap.
    /// </summary>
    /// <param name="x">The horizontal tile coordinate.</param>
    /// <param name="y">The vertical tile coordinate.</param>
    /// <returns>The requested value, or <see langword="null"/> when it is unavailable.</returns>
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

    /// <summary>
    /// Gets all cached frame bitmaps in this region.
    /// </summary>
    /// <returns>A dictionary keyed by frame coordinates.</returns>
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

    /// <summary>
    /// Gets all cached frame images in this region.
    /// </summary>
    /// <returns>A dictionary keyed by frame coordinates.</returns>
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
    /// <param name="x">The horizontal tile coordinate.</param>
    /// <param name="y">The vertical tile coordinate.</param>
    /// <returns>The resulting value.</returns>
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
    /// Attempts to get the explicit collision adjustment assigned to one frame.
    /// </summary>
    /// <param name="x">The horizontal tile coordinate.</param>
    /// <param name="y">The vertical tile coordinate.</param>
    /// <param name="collisionAdjust">The collision adjust.</param>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Returns <see langword="true"/> when an explicit frame-level override exists.
    /// When <see langword="false"/>, no override exists (the frame inherits <see cref="CollisionAdjust"/>).
    /// The <paramref name="collisionAdjust"/> out parameter is only meaningful when the method returns <see langword="true"/>.
    /// </remarks>
    public bool TryGetFrameCollisionAdjustOverride(
        int x,
        int y,
        out CollisionAdjust collisionAdjust)
    {
        ThrowIfDisposed();

        if (_tileCache is null)
            BuildTileCache();

        if (!IsFrameCoordinateValid(x, y))
        {
            collisionAdjust = default;
            return false;
        }

        return _frameCollisionAdjustments.TryGetValue(
            (x, y),
            out collisionAdjust);
    }

    /// <summary>
    /// Sets the collision adjustment for one frame and updates its cache entry.
    /// </summary>
    /// <param name="x">The horizontal tile coordinate.</param>
    /// <param name="y">The vertical tile coordinate.</param>
    /// <param name="collisionAdjust">The collision adjust.</param>
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

        _frameCollisionAdjustments[(x, y)] = collisionAdjust;

        if (_tileCache![x, y] is { } slice)
            _tileCache[x, y] = slice.WithCollisionAdjust(collisionAdjust);
    }

    /// <summary>
    /// Removes a frame-level collision adjustment so the frame once again inherits
    /// <see cref="CollisionAdjust"/>.
    /// </summary>
    /// <param name="x">The horizontal tile coordinate.</param>
    /// <param name="y">The vertical tile coordinate.</param>
    /// <returns>
    /// <see langword="true"/> when an explicit frame override was removed;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool ClearFrameCollisionAdjustOverride(int x, int y)
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

        if (!_frameCollisionAdjustments.Remove((x, y)))
            return false;

        if (_tileCache![x, y] is { } slice)
            _tileCache[x, y] = slice.WithCollisionAdjust(_collisionAdjust);

        return true;
    }

    /// <summary>
    /// Gets the frame-local collision rectangle for one frame.
    /// </summary>
    /// <param name="x">The horizontal tile coordinate.</param>
    /// <param name="y">The vertical tile coordinate.</param>
    /// <returns>The resulting value.</returns>
    public Rectangle GetFrameCollisionArea(int x, int y) =>
        GetFrameCollisionAdjust(x, y)
            .ApplyTo(new Rectangle(Point.Empty, _tileSize));

    /// <summary>
    /// Gets the effective collision type for one frame.
    /// </summary>
    /// <param name="x">The horizontal tile coordinate.</param>
    /// <param name="y">The vertical tile coordinate.</param>
    /// <returns>The resulting value.</returns>
    public TileCollisionType GetFrameCollisionType(int x, int y)
    {
        ThrowIfDisposed();

        if (!IsFrameCoordinateValid(x, y))
            return _collisionType;

        return _frameCollisionTypes.TryGetValue((x, y), out var collisionType)
            ? collisionType
            : _collisionType;
    }

    /// <summary>
    /// Attempts to get the explicit collision type assigned to one frame.
    /// </summary>
    /// <param name="x">The horizontal tile coordinate.</param>
    /// <param name="y">The vertical tile coordinate.</param>
    /// <param name="collisionType">The collision type.</param>
    /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
    public bool TryGetFrameCollisionTypeOverride(
        int x,
        int y,
        out TileCollisionType collisionType)
    {
        ThrowIfDisposed();

        if (_tileCache is null)
            BuildTileCache();

        if (!IsFrameCoordinateValid(x, y))
        {
            collisionType = default;
            return false;
        }

        return _frameCollisionTypes.TryGetValue((x, y), out collisionType);
    }

    /// <summary>
    /// Sets an explicit collision type for one frame.
    /// </summary>
    /// <param name="x">The horizontal tile coordinate.</param>
    /// <param name="y">The vertical tile coordinate.</param>
    /// <param name="collisionType">The collision type.</param>
    public void SetFrameCollisionType(int x, int y, TileCollisionType collisionType)
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

        _frameCollisionTypes[(x, y)] = collisionType;
    }

    /// <summary>
    /// Removes a frame-level collision type so the frame once again inherits
    /// <see cref="CollisionType"/>.
    /// </summary>
    /// <param name="x">The horizontal tile coordinate.</param>
    /// <param name="y">The vertical tile coordinate.</param>
    /// <returns>The resulting value.</returns>
    public bool ClearFrameCollisionTypeOverride(int x, int y)
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

        return _frameCollisionTypes.Remove((x, y));
    }

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
            _regionMargin.Right < -_tilePadding.Right || _regionMargin.Bottom < -_tilePadding.Bottom)
        {
            throw new InvalidOperationException("Leading region margins cannot be negative; trailing margins may only cancel trailing tile padding.");
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
        PruneFrameCollisionTypes(xTiles, yTiles);
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

    /// <summary>
    /// Disposes and clears all cached frame bitmap and image slices.
    /// </summary>
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

    /// <summary>
    /// Gets the source-image rectangle for a frame in this region.
    /// </summary>
    /// <param name="xTile">The horizontal frame coordinate.</param>
    /// <param name="yTile">The vertical frame coordinate.</param>
    /// <returns>The frame source rectangle, or <see cref="Rectangle.Empty"/> when the coordinates are invalid.</returns>
    internal Rectangle GetFrameSourceBounds(int xTile, int yTile)
    {
        ThrowIfDisposed();

        if (_tileCache is null)
            BuildTileCache();

        return IsFrameCoordinateValid(xTile, yTile)
            ? GetTileBounds(xTile, yTile)
            : Rectangle.Empty;
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

    private void ApplyDefaultCollisionAdjustToCache()
    {
        if (_tileCache is null)
            return;

        for (int y = 0; y < _tileCache.GetLength(1); y++)
        {
            for (int x = 0; x < _tileCache.GetLength(0); x++)
            {
                if (!_frameCollisionAdjustments.ContainsKey((x, y)) &&
                    _tileCache[x, y] is { } slice)
                {
                    _tileCache[x, y] = slice.WithCollisionAdjust(_collisionAdjust);
                }
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

    private void PruneFrameCollisionTypes(int columns, int rows)
    {
        foreach (var key in _frameCollisionTypes.Keys.ToArray())
        {
            if ((uint)key.x >= (uint)columns || (uint)key.y >= (uint)rows)
                _frameCollisionTypes.Remove(key);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TilesheetRegion));
    }

    /// <summary>
    /// Releases resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ClearTileCache();
        _frameCollisionAdjustments.Clear();
        _frameCollisionTypes.Clear();
    }
}
