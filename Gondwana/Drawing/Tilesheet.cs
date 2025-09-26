using System.Drawing;
using Newtonsoft.Json;
using Gondwana.Resource;
using Gondwana.Skia;
using SkiaSharp;

namespace Gondwana.Drawing;

/// <summary>
/// Represents a tilesheet image and metadata for rendering tiles.
/// </summary>
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

    private Tilesheet() { }

    public Tilesheet(string name, SKBitmap bitmap)
    {
        _name = name;
        SkBitmap = bitmap;
        _tilesheets[_name] = this;
    }

    public Tilesheet(string name, Stream stream)
        : this(name, SKBitmap.Decode(stream)) { }

    public Tilesheet(string name, string file)
        : this(name, SKBitmap.Decode(file))
    {
        ImageFilePath = file;
    }

    public Tilesheet(EngineResourceFile resFile, string entryName)
    {
        ResourceIdentifier = new EngineResourceFileIdentifier(resFile, EngineResourceFileTypes.Image, entryName);
        _name = entryName;
        SkBitmap = SKBitmap.Decode(ResourceIdentifier.Data);
        ClearTilesheet(_name);  // clear previous instance if exists
        _tilesheets[_name] = this;
    }

    public Tilesheet(Tilesheet baseSheet, string name, string file)
    {
        InitialOffsetX = baseSheet.InitialOffsetX;
        InitialOffsetY = baseSheet.InitialOffsetY;
        XPixelsBetweenTiles = baseSheet.XPixelsBetweenTiles;
        YPixelsBetweenTiles = baseSheet.YPixelsBetweenTiles;
        _tileSize = baseSheet._tileSize;
        _overlapTopSpace = baseSheet._overlapTopSpace;
        ValueBag = new(baseSheet.ValueBag);
        _name = name;
        SkBitmap = SKBitmap.Decode(file);
        ImageFilePath = file;
        _tilesheets[_name] = this;
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
            _tilesheets.Remove(_name);
            _name = value;
            _tilesheets[_name] = this;
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
            RecalcMaxOverlapRatio();
            BuildTileCache();
        }
    }

    [JsonProperty]
    private int _overlapTopSpace;

    [JsonIgnore]
    public int OverlappingTopSpace
    {
        get => _overlapTopSpace;
        set
        {
            _overlapTopSpace = value;
            RecalcMaxOverlapRatio();
            BuildTileCache();
        }
    }

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
    public int PrimaryHeight => _tileSize.Height - _overlapTopSpace;
    
    [JsonIgnore]
    public float OverlapTopSpaceToPrimaryRatio => (float)_overlapTopSpace / PrimaryHeight;

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
        int x = (xTile * (_tileSize.Width + XPixelsBetweenTiles)) + InitialOffsetX;
        int y = (yTile * (_tileSize.Height + YPixelsBetweenTiles)) + InitialOffsetY;
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
        if (_tileCache == null) return;

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
        if (_tileCache == null) BuildTileCache();
        return _tileCache?[x, y]?.Image;
    }

    public SKBitmap? GetBitmap(int x, int y)
    {
        if (_tileCache == null) BuildTileCache();
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

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _tilesheets.Remove(_name);
        RecalcMaxOverlapRatio();
        ClearCache();
        SkBitmap?.Dispose();
        SkBitmapOriginal?.Dispose();
        Disposed?.Invoke(this, new TilesheetDisposedEventArgs(this));
    }

    #region static
    internal readonly static Dictionary<string, Tilesheet> _tilesheets = new();

    public static int Count => _tilesheets.Count;
    public static List<Tilesheet> GetAllTilesheets() => _tilesheets.Values.ToList();
    public static List<string> GetTilesheetKeys() => _tilesheets.Keys.ToList();
    public static Tilesheet? GetTilesheet(string name) => _tilesheets.TryGetValue(name, out var ts) ? ts : null;

    public static void ClearTilesheet(string name)
    {
        if (_tilesheets.TryGetValue(name, out var ts))
            ts.Dispose();
    }

    public static void ClearAllTilesheets()
    {
        foreach (var ts in _tilesheets.Values.ToList())
            ts.Dispose();
    }

    public static float MaxOverlappingTopSpaceRatio { get; private set; }

    private static void RecalcMaxOverlapRatio()
    {
        MaxOverlappingTopSpaceRatio = _tilesheets.Values.Count == 0 ? 0 : _tilesheets.Values.Max(ts => ts.OverlapTopSpaceToPrimaryRatio);
    }
    #endregion
}
