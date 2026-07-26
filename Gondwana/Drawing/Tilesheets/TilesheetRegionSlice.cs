using Gondwana.Physics.Collisions;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Drawing.Tilesheets;

/// <summary>
/// Represents a cached tilesheet-region slice and its per-frame collision metadata.
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
    /// The collision adjustment associated with this cached frame.
    /// </summary>
    public readonly CollisionAdjust CollisionAdjust;

    /// <summary>
    /// Gets the frame-local collision rectangle.
    /// </summary>
    public readonly Rectangle CollisionArea =>
        CollisionAdjust.ApplyTo(new Rectangle(0, 0, Bitmap.Width, Bitmap.Height));

    /// <summary>
    /// Initializes a new cached tilesheet slice.
    /// </summary>
    public TilesheetRegionSlice(
        SKBitmap bmp,
        SKImage img,
        CollisionAdjust collisionAdjust)
    {
        Bitmap = bmp;
        Image = img;
        CollisionAdjust = collisionAdjust;
    }

    /// <summary>
    /// Returns a cache entry that reuses the image resources with updated collision metadata.
    /// </summary>
    public readonly TilesheetRegionSlice WithCollisionAdjust(CollisionAdjust collisionAdjust) =>
        new(Bitmap, Image, collisionAdjust);
}
