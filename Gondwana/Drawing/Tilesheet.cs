using System.Drawing;
using System.Text.Json.Serialization;
using Gondwana.Rendering;
using Gondwana.Resource;
using SkiaSharp;

namespace Gondwana.Drawing;

/// <summary>
/// Represents a tilesheet image and metadata for rendering tiles.
/// </summary>
public sealed class Tilesheet : IDisposable
{
    private SKBitmap?[,]? _tileCache;

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

    [JsonInclude]
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

    [JsonInclude]
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

    [JsonInclude]
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

    [JsonInclude]
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

    [JsonInclude]
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

    [JsonInclude]
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

    [JsonInclude]
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

    [JsonInclude]
    public Dictionary<string, string> ValueBag = new();

    [JsonInclude]
    public EngineResourceFileIdentifier? ResourceIdentifier { get; private set; }

    [JsonInclude]
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

        ApplyAlphaMaskInPlace(SkBitmap, targetColor, tolerance);
        BuildTileCache();
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

        _tileCache = new SKBitmap[xTiles, yTiles];

        for (int y = 0; y < yTiles; y++)
        {
            for (int x = 0; x < xTiles; x++)
            {
                var srcRect = GetTileBounds(x, y);
                if (!SkBitmap.Info.Rect.Contains(srcRect.ToSKRectI()))
                    continue;

                var subset = new SKBitmap(_tileSize.Width, _tileSize.Height);
                if (SkBitmap.ExtractSubset(subset, srcRect.ToSKRectI()))
                    _tileCache[x, y] = subset;
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
                _tileCache[x, y]?.Dispose();
                _tileCache[x, y] = null;
            }
        }

        _tileCache = null;
    }

    public SKBitmap? this[int x, int y]
    {
        get
        {
            if (_tileCache == null)
                BuildTileCache();

            if (x < 0 || y < 0 || x >= _tileCache!.GetLength(0) || y >= _tileCache.GetLength(1))
                return null;

            return _tileCache?[x, y];
        }
    }

    public Dictionary<(int x, int y), SKBitmap> GetAllTiles()
    {
        if (_tileCache == null)
            BuildTileCache();

        var tiles = new Dictionary<(int x, int y), SKBitmap>();

        int xTiles = _tileCache!.GetLength(0);
        int yTiles = _tileCache.GetLength(1);

        for (int y = 0; y < yTiles; y++)
        {
            for (int x = 0; x < xTiles; x++)
            {
                var bmp = _tileCache[x, y];
                if (bmp != null)
                    tiles[(x, y)] = bmp;
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
        SkBitmap.Dispose();
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

    public static void ApplyAlphaMaskInPlace(SKBitmap bitmap, SKColor targetColor, byte tolerance = 0)
    {
        if (bitmap == null || bitmap.IsEmpty)
            throw new ArgumentException("Invalid bitmap.");

        int width = bitmap.Width;
        int height = bitmap.Height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = bitmap.GetPixel(x, y);

                if (IsColorClose(color, targetColor, tolerance))
                {
                    // Make the pixel fully transparent
                    var newColor = new SKColor(color.Red, color.Green, color.Blue, 0);
                    bitmap.SetPixel(x, y, newColor);
                }
                else
                {
                    // Optional: ensure opaque for all other pixels
                    if (color.Alpha != 255)
                    {
                        var opaqueColor = new SKColor(color.Red, color.Green, color.Blue, 255);
                        bitmap.SetPixel(x, y, opaqueColor);
                    }
                }
            }
        }

        bitmap.NotifyPixelsChanged(); // Useful if it's shared with GPU
    }

    private static bool IsColorClose(SKColor a, SKColor b, byte tolerance)
    {
        return
            Math.Abs(a.Red - b.Red) <= tolerance &&
            Math.Abs(a.Green - b.Green) <= tolerance &&
            Math.Abs(a.Blue - b.Blue) <= tolerance;
    }
    #endregion
}
