using System.Drawing;
using System.Runtime.Serialization;
using SkiaSharp;

namespace Gondwana.Drawing;

/// <summary>
/// Represents the source Tilesheet and its coordinates to render on a destination.
/// </summary>
[DataContract]
public struct Frame
{
    [DataMember]
    public readonly Tilesheet Tilesheet;

    [DataMember]
    public readonly int XTile;      // xTile * bmp.TileWidth = starting point for source bitmap

    [DataMember]
    public readonly int YTile;      // yTile * bmp.TileHeight = starting point for source bitmap

    private SKBitmap? _cachedSkBitmap;

    [IgnoreDataMember]
    public Size Size => Tilesheet.TileSize;

    public Frame(Tilesheet tilesheet, int xTile, int yTile)
    {
        Tilesheet = tilesheet;
        XTile = xTile;
        YTile = yTile;
        _cachedSkBitmap = null;
    }

    public Bitmap GetBitmap()
    {
        var sourceRect = Tilesheet.GetSourceRange(XTile, YTile);

        if (new Rectangle(Point.Empty, Tilesheet.Bmp.Size).Contains(sourceRect))
            return Tilesheet.Bmp.Clone(sourceRect, Tilesheet.Bmp.PixelFormat);
        else
            return null;
    }

    public Bitmap GetBitmapMask()
    {
        if (Tilesheet.Mask == null)
            return null;

        var sourceRect = Tilesheet.Mask.GetSourceRange(XTile, YTile);
        return Tilesheet.Mask.Bmp.Clone(sourceRect, Tilesheet.Mask.Bmp.PixelFormat);
    }

    public SKBitmap? GetSkiaBitmap()
    {
        if (_cachedSkBitmap != null)
            return _cachedSkBitmap;

        var color = GetBitmap();
        if (color == null)
            return null;

        var mask = GetBitmapMask();
        _cachedSkBitmap = Gondwana.Rendering.Backbuffer.CombineBitmapWithMask(color, mask);
        return _cachedSkBitmap;
    }

    public static bool operator ==(Frame f1, Frame f2)
    {
        return f1.Tilesheet.Equals(f2.Tilesheet) && f1.XTile == f2.XTile && f1.YTile == f2.YTile;
    }

    public static bool operator !=(Frame f1, Frame f2)
    {
        return !(f1 == f2);
    }

    public override bool Equals(object? obj)
    {
        if (obj is Frame other)
            return this == other;
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Tilesheet, XTile, YTile);
    }

    public override string ToString()
    {
        return $"{Tilesheet.Name} / x:{XTile} / y:{YTile}";
    }
}
