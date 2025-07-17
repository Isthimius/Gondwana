using Gondwana.Resource;
using SkiaSharp;
using System.Drawing;
using System.Text.Json.Serialization;

namespace Gondwana.Drawing;

/// <summary>
/// Represents a tilesheet image and metadata for rendering tiles.
/// </summary>
public sealed class Tilesheet : IDisposable
{
    public event TilesheetDisposedHandler? Disposed;

    [JsonInclude] public int InitialOffsetX;
    [JsonInclude] public int InitialOffsetY;
    [JsonInclude] public int XPixelsBetweenTiles;
    [JsonInclude] public int YPixelsBetweenTiles;
    [JsonInclude] public Tilesheet? Mask;

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
        Mask = baseSheet.Mask;
        _tileSize = baseSheet._tileSize;
        _extraTopSpace = baseSheet._extraTopSpace;
        ValueBag = new(baseSheet.ValueBag);
        _name = name;
        _skBitmap = SKBitmap.Decode(file);
        ImageFilePath = file;
        _tilesheets[_name] = this;
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
        }
    }

    [JsonIgnore] public int PrimaryHeight => _tileSize.Height - _extraTopSpace;
    [JsonIgnore] public float ExtraTopSpaceToPrimaryRatio => (float)_extraTopSpace / PrimaryHeight;
    [JsonIgnore] public string MaskName => Mask?.Name ?? string.Empty;

    public Rectangle GetSourceRange(int xTile, int yTile)
    {
        int x = (xTile * (_tileSize.Width + XPixelsBetweenTiles)) + InitialOffsetX;
        int y = (yTile * (_tileSize.Height + YPixelsBetweenTiles)) + InitialOffsetY;
        return new Rectangle(new Point(x, y), _tileSize);
    }

    public List<Frame> GetFrames()
    {
        var frames = new List<Frame>();
        int xTile = 0, yTile = 0;
        var range = GetSourceRange(xTile, yTile);
        int x = range.X, y = range.Y;

        while (y < _skBitmap.Height)
        {
            while (x < _skBitmap.Width)
            {
                frames.Add(new Frame(this, xTile, yTile));
                range = GetSourceRange(++xTile, yTile);
                x = range.X;
            }
            xTile = 0;
            range = GetSourceRange(xTile, ++yTile);
            x = range.X;
            y = range.Y;
        }

        return frames;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Tilesheet._tilesheets.Remove(_name);
        RecalcMaxOverlapRatio();
        _skBitmap.Dispose();
        Disposed?.Invoke(new TilesheetDisposedEventArgs(this));
    }

    internal static Dictionary<string, Tilesheet> _tilesheets = new();

    public static int Count => _tilesheets.Count;
    public static List<Tilesheet> AllTilesheets => _tilesheets.Values.ToList();
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
}