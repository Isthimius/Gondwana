using System.Drawing;
using SkiaSharp;

namespace Gondwana.Drawing.Direct.ImageLayer;

/// <summary>
/// Represents a single image instance rendered by an <see cref="ImageInstanceLayer"/>.
/// </summary>
/// <remarks>
/// <para>
/// An image instance is a lightweight visual object consisting of a bitmap, bounds,
/// optional velocity, optional rotation, and tint.
/// </para>
/// <para>
/// This type is intended for larger, longer-lived visual elements such as drifting clouds,
/// fog patches, leaves, embers, or similar ambient image-based effects.
/// </para>
/// </remarks>
public sealed class ImageInstance
{
    /// <summary>
    /// Gets or sets the bitmap rendered for this instance.
    /// </summary>
    public required SKBitmap Bitmap { get; set; }

    /// <summary>
    /// Gets or sets the destination bounds of the image in the coordinate space used by
    /// the containing <see cref="ImageInstanceLayer"/>.
    /// </summary>
    /// <remarks>
    /// Bounds are world pixels in scene-layer mode and screen pixels in view mode.
    /// </remarks>
    public required RectangleF Bounds { get; set; }

    /// <summary>
    /// Gets or sets the horizontal velocity in pixels per second.
    /// </summary>
    public float VelocityX { get; set; }

    /// <summary>
    /// Gets or sets the vertical velocity in pixels per second.
    /// </summary>
    public float VelocityY { get; set; }

    /// <summary>
    /// Gets or sets the current rotation in degrees.
    /// </summary>
    public float Rotation { get; set; }

    /// <summary>
    /// Gets or sets the angular velocity in degrees per second.
    /// </summary>
    public float AngularVelocity { get; set; }

    /// <summary>
    /// Gets or sets the tint applied when rendering the image.
    /// </summary>
    /// <remarks>
    /// Defaults to white, which leaves the bitmap colors unchanged.
    /// Alpha may be used to apply transparency.
    /// </remarks>
    public SKColor Tint { get; set; } = SKColors.White;

    /// <summary>
    /// Gets or sets an optional user-defined tag associated with this instance.
    /// </summary>
    public object? Tag { get; set; }
}