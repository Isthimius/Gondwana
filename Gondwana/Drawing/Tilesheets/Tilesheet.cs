using System.Drawing;
using Gondwana.Resource;
using Gondwana.Skia;
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

    public event EventHandler<TilesheetDisposedEventArgs> Disposed;

    private Tilesheet()
    { }

    public Tilesheet(string name, SKBitmap bitmap)
        : this()
    {
        _name = name;
        SkBitmap = bitmap;
        TilesheetRegistry.Instance.Register(this);
    }

    public Tilesheet(string name, Stream stream)
        : this(name, SKBitmap.Decode(stream) ?? throw new ArgumentException("Invalid image stream.")) { }

    public Tilesheet(string name, string file)
        : this(name, SKBitmap.Decode(file) ?? throw new ArgumentException($"Invalid image file: {file}"))
    {
        ImageFilePath = file;
    }

    public Tilesheet(EngineResourceFile resFile, string entryName)
    {
        ResourceIdentifier = new EngineResourceFileIdentifier(resFile, EngineResourceFileTypes.Image, entryName);
        _name = entryName;
        SkBitmap = SKBitmap.Decode(ResourceIdentifier.Data);
        TilesheetRegistry.Instance.Register(this);
    }

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

    [JsonIgnore]
    public SKBitmap SkBitmap { get; private set; }

    [JsonIgnore]
    public SKBitmap? SkBitmapOriginal { get; private set; } = null;

    [JsonProperty]
    private string _name = string.Empty;

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

    [JsonProperty]
    public int OverhangPixels { get; set; } = 0;

    [JsonProperty]
    private int _initialOffsetX;

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

    [JsonProperty]
    public Dictionary<string, string> ValueBag = new();

    [JsonProperty]
    public EngineResourceFileIdentifier? ResourceIdentifier { get; private set; }

    [JsonProperty]
    public string ImageFilePath { get; private set; } = string.Empty;

    [JsonIgnore]
    public int PrimaryHeight => _tileSize.Height - OverhangPixels;

    public void ApplyMask(SKColor? maskColor = null, byte tolerance = 0)
    {
        if (SkBitmap == null || SkBitmap.IsEmpty)
            throw new ArgumentException("Invalid bitmap.");

        var targetColor = maskColor ?? SKColors.White;

        SkBitmapOriginal = SkBitmap.Copy();
        SkiaHelper.ApplyAlphaMask(SkBitmap, targetColor, tolerance);
        BuildTileCache();
    }

    public void ApplyPremultiplyAlpha()
    {
        if (SkBitmap == null || SkBitmap.IsEmpty)
            throw new ArgumentException("Invalid bitmap.");

        SkBitmapOriginal = SkBitmap.Copy();
        SkBitmap = SkiaHelper.PremultiplyAlpha(SkBitmap);
        BuildTileCache();
    }

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

                var bmp = new SKBitmap(_tileSize.Width, _tileSize.Height);
                if (SkBitmap.ExtractSubset(bmp, srcRect.ToSKRectI()))
                {
                    var img = SKImage.FromBitmap(bmp);
                    _tileCache[x, y] = new TilesheetSlice(bmp, img);
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

    // --- IDisposable pattern ---
    private bool _disposed;

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