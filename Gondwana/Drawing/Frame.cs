using System.Drawing;
using System.Runtime.Serialization;

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

    [IgnoreDataMember]
    public Size Size
    {
        get { return Tilesheet.TileSize; }
    }

    public Frame(Tilesheet tilesheet, int xTile, int yTile)
    {
        Tilesheet = tilesheet;
        XTile = xTile;
        YTile = yTile;
    }

    public Bitmap GetBitmap()
    {
        var sourceRect = Tilesheet.GetSourceRange(XTile, YTile);

        if (new Rectangle(new Point(), Tilesheet.Bmp.Size).Contains(sourceRect))
            return Tilesheet.Bmp.Clone(sourceRect, Tilesheet.Bmp.PixelFormat);
        else
            return null;
    }

    public Bitmap GetBitmapMask()
    {
        if (Tilesheet.Mask == null)
            return null;

        var sourceRect = Tilesheet.Mask.GetSourceRange(XTile, YTile);
        var bmp = Tilesheet.Mask.Bmp.Clone(sourceRect, Tilesheet.Mask.Bmp.PixelFormat);
        return bmp;
    }
    
    public static bool operator ==(Frame f1, Frame f2)
    {
        return f1.Tilesheet.Equals(f2.Tilesheet) && f1.XTile == f2.XTile && f1.YTile == f2.YTile;
    }

    public static bool operator !=(Frame f1, Frame f2)
    {
        return !(f1.Tilesheet.Equals(f2.Tilesheet) && f1.XTile == f2.XTile && f1.YTile == f2.YTile);
    }

    public override string ToString()
    {
        return string.Format("{0} / x:{1} / y:{2}", Tilesheet.Name, XTile, YTile);
    }
}
