using SkiaSharp;
using System.Drawing;
using System.Text.Json.Serialization;

namespace Gondwana.Drawing;

/// <summary>
/// Represents the source Tilesheet and its coordinates to render on a destination.
/// </summary>
public struct Frame
{
    [JsonInclude]
    public readonly Tilesheet Tilesheet;

    [JsonInclude]
    public readonly int XTile;

    [JsonInclude]
    public readonly int YTile;

    public Frame(Tilesheet tilesheet, int xTile, int yTile)
    {
        Tilesheet = tilesheet;
        XTile = xTile;
        YTile = yTile;
    }

    [JsonIgnore]
    public readonly Size Size => Tilesheet.TileSize;

    [JsonIgnore]
    public readonly SKBitmap? SkBitmap => Tilesheet?[XTile, YTile]?.Bitmap;

    [JsonIgnore]
    public readonly SKImage? SkImage => Tilesheet?[XTile, YTile]?.Image;
}
