using SkiaSharp;

namespace Gondwana.Drawing.Tilesheets;

/// <summary>
/// Represents a cached slice of a tilesheet region containing both bitmap and image representations of a single tile.
/// </summary>
internal readonly struct TilesheetRegionSlice
{
    /// <summary>
    /// The bitmap representation of the tile slice.
    /// </summary>
    public readonly SKBitmap Bitmap;

    /// <summary>
    /// The image representation of the tile slice.
    /// </summary>
    public readonly SKImage Image;

    /// <summary>
    /// Initializes a new instance of the <see cref="TilesheetRegionSlice"/> struct with the specified bitmap and image.
    /// </summary>
    /// <param name="bmp">The SKBitmap representation of the tile.</param>
    /// <param name="img">The SKImage representation of the tile.</param>
    public TilesheetRegionSlice(SKBitmap bmp, SKImage img)
    {
        Bitmap = bmp;
        Image = img;
    }
}
