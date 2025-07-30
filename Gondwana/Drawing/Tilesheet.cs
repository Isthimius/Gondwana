using System.Collections.Concurrent;
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

    [JsonInclude] public int InitialOffsetX;
    [JsonInclude] public int InitialOffsetY;
    [JsonInclude] public int XPixelsBetweenTiles;
    [JsonInclude] public int YPixelsBetweenTiles;

    [JsonInclude] private Size _tileSize;
    [JsonInclude] private string _name = string.Empty;
    [JsonInclude] private int _extraTopSpace;
    [JsonInclude] public Dictionary<string, string> ValueBag = new();
    [JsonInclude] public EngineResourceFileIdentifier? ResourceIdentifier { get; private set; }
    [JsonInclude] public string ImageFilePath { get; private set; } = string.Empty;

    private SKBitmap _skBitmap = null!;

    private Tilesheet() { }

    public Tilesheet(string name, SKBitmap bitmap)
    {
        _name = name;
        _skBitmap = bitmap;
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
        _skBitmap = SKBitmap.Decode(ResourceIdentifier.Data);
        _tilesheets[_name] = this;
    }

    public Tilesheet(Tilesheet baseSheet, string name, string file)
    {
        InitialOffsetX = baseSheet.InitialOffsetX;
        InitialOffsetY = baseSheet.InitialOffsetY;
        XPixelsBetweenTiles = baseSheet.XPixelsBetweenTiles;
        YPixelsBetweenTiles = baseSheet.YPixelsBetweenTiles;
        _tileSize = baseSheet._tileSize;
        _extraTopSpace = baseSheet._extraTopSpace;
        ValueBag = new(baseSheet.ValueBag);
        _name = name;
        _skBitmap = SKBitmap.Decode(file);
        ImageFilePath = file;
        _tilesheets[_name] = this;

        CacheTiles();
    }

    private void CacheTiles()
    {
        ClearCache();

        int xTiles = (_skBitmap.Width - InitialOffsetX + XPixelsBetweenTiles) / (_tileSize.Width + XPixelsBetweenTiles);
        int yTiles = (_skBitmap.Height - InitialOffsetY + YPixelsBetweenTiles) / (_tileSize.Height + YPixelsBetweenTiles);

        _tileCache = new SKBitmap[xTiles, yTiles];

        for (int y = 0; y < yTiles; y++)
        {
            for (int x = 0; x < xTiles; x++)
            {
                var srcRect = GetSourceRange(x, y);
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

    [JsonIgnore] public SKBitmap SkBitmap => _skBitmap;

    [JsonIgnore]
    public Size TileSize
    {
        get => _tileSize;
        set
        {
            _tileSize = value;
            RecalcMaxOverlapRatio();
            CacheTiles();
        }
    }

    [JsonIgnore]
    public int ExtraTopSpace
    {
        get => _extraTopSpace;
        set
        {
            _extraTopSpace = value;
            RecalcMaxOverlapRatio();
            CacheTiles();
        }
    }

    [JsonIgnore] public int PrimaryHeight => _tileSize.Height - _extraTopSpace;
    [JsonIgnore] public float ExtraTopSpaceToPrimaryRatio => (float)_extraTopSpace / PrimaryHeight;

    private Rectangle GetSourceRange(int xTile, int yTile)
    {
        int x = (xTile * (_tileSize.Width + XPixelsBetweenTiles)) + InitialOffsetX;
        int y = (yTile * (_tileSize.Height + YPixelsBetweenTiles)) + InitialOffsetY;
        return new Rectangle(new Point(x, y), _tileSize);
    }

    public SKBitmap? this[int x, int y]
    {
        get => _tileCache?[x, y];
    }

    public Dictionary<(int x, int y), SKBitmap> GetAllTiles()
    {
        if (_tileCache == null)
            throw new InvalidOperationException("Tile cache has not been initialized.");

        var frames = new Dictionary<(int x, int y), SKBitmap>();

        int xTiles = _tileCache.GetLength(0);
        int yTiles = _tileCache.GetLength(1);

        for (int y = 0; y < yTiles; y++)
        {
            for (int x = 0; x < xTiles; x++)
            {
                var bmp = _tileCache[x, y];
                if (bmp != null)
                    frames[(x, y)] = bmp;
            }
        }

        return frames;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Tilesheet._tilesheets.Remove(_name);
        RecalcMaxOverlapRatio();
        ClearCache();
        _skBitmap.Dispose();
        Disposed?.Invoke(this, new TilesheetDisposedEventArgs(this));
    }

    #region static
    internal static Dictionary<string, Tilesheet> _tilesheets = new();

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

    public static float MaxExtraTopSpaceRatio { get; private set; }

    private static void RecalcMaxOverlapRatio()
    {
        MaxExtraTopSpaceRatio = _tilesheets.Values.Count == 0 ? 0 : _tilesheets.Values.Max(ts => ts.ExtraTopSpaceToPrimaryRatio);
    }
    #endregion
}