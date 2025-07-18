using Gondwana.Rendering;
using SkiaSharp;
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
    public readonly int XTile;

    [DataMember]
    public readonly int YTile;

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

    // TODO: this is causing issues with .Snapshot()
    public SKBitmap? GetSkiaBitmap()
    {
        if (_cachedSkBitmap != null)
            return _cachedSkBitmap;

        if (Tilesheet is null) return null;

        var srcRect = Tilesheet.GetSourceRange(XTile, YTile);

        if (!Tilesheet.SkBitmap.Info.Rect.Contains(srcRect.ToSKRectI()))
            return null;

        // Create a temporary surface and draw the tile region onto it
        using var surface = SKSurface.Create(new SKImageInfo(srcRect.Width, srcRect.Height));
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawBitmap(
            Tilesheet.SkBitmap,
            srcRect.ToSKRect(),
            new SKRect(0, 0, srcRect.Width, srcRect.Height)
        );

        // Handle the mask (if present)
        SKBitmap? croppedMask = null;
        if (Tilesheet.Mask?.SkBitmap is SKBitmap maskBitmap &&
            maskBitmap.Info.Rect.Contains(srcRect.ToSKRectI()))
        {
            croppedMask = new SKBitmap(srcRect.Width, srcRect.Height);

            using var canvas = new SKCanvas(croppedMask);
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(maskBitmap, srcRect.ToSKRect(), new SKRect(0, 0, srcRect.Width, srcRect.Height));
        }

        // Encode the surface as an image to memory and decode back into SKBitmap
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;

        using var cropped = SKBitmap.Decode(stream);

        // Combine with optional mask
        _cachedSkBitmap = Gondwana.Rendering.Backbuffer.CombineBitmapWithMask(cropped, croppedMask);
        return _cachedSkBitmap;
    }

    public static bool operator ==(Frame f1, Frame f2)
        => f1.Tilesheet.Equals(f2.Tilesheet) && f1.XTile == f2.XTile && f1.YTile == f2.YTile;

    public static bool operator !=(Frame f1, Frame f2)
        => !(f1 == f2);

    public override bool Equals(object? obj)
        => obj is Frame other && this == other;

    public override int GetHashCode()
        => HashCode.Combine(Tilesheet, XTile, YTile);

    public override string ToString()
        => $"{Tilesheet.Name} / x:{XTile} / y:{YTile}";
}
