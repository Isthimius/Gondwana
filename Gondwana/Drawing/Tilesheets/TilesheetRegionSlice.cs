using SkiaSharp;

namespace Gondwana.Drawing.Tilesheets;

internal readonly struct TilesheetRegionSlice
{
    public readonly SKBitmap Bitmap;
    public readonly SKImage Image;

    public TilesheetRegionSlice(SKBitmap bmp, SKImage img)
    {
        Bitmap = bmp;
        Image = img;
    }
}
