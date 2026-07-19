using SkiaSharp;

namespace Gondwana.Rendering;

/// <summary>
/// Represents the abstract base class for render surface adapters that present backbuffer output
/// to a destination surface.
/// </summary>
public abstract class RenderSurfaceAdapterBase
{
    /// <summary>
    /// Occurs when the render surface adapter is resized.
    /// </summary>
    /// <remarks>
    /// This event is raised when the <see cref="Width"/> or <see cref="Height"/> properties change,
    /// providing both the old and new dimensions in the event arguments.
    /// </remarks>
    public event Action<RenderSurfaceAdapterResizedEventArgs>? Resized;

    /// <summary>
    /// Gets the current width of the render surface in pixels.
    /// </summary>
    /// <value>The width of the render surface.</value>
    public int Width { get; protected set; }

    /// <summary>
    /// Gets the current height of the render surface in pixels.
    /// </summary>
    /// <value>The height of the render surface.</value>
    public int Height { get; protected set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderSurfaceAdapterBase"/> class with the specified dimensions.
    /// </summary>
    /// <param name="destWidth">The initial width of the render surface in pixels.</param>
    /// <param name="destHeight">The initial height of the render surface in pixels.</param>
    protected RenderSurfaceAdapterBase(int destWidth, int destHeight)
    {
        SetDestinationSize(destWidth, destHeight);
    }

    /// <summary>
    /// Sets the destination size of the render surface and raises the <see cref="Resized"/> event if the dimensions have changed.
    /// </summary>
    /// <param name="destWidth">The new width of the render surface in pixels.</param>
    /// <param name="destHeight">The new height of the render surface in pixels.</param>
    /// <remarks>
    /// If the specified dimensions are the same as the current dimensions, this method returns without making changes
    /// or raising the <see cref="Resized"/> event.
    /// </remarks>
    protected void SetDestinationSize(int destWidth, int destHeight)
    {
        if (destWidth == Width && destHeight == Height)
            return;

        var oldWidth = Width;
        var oldHeight = Height;

        Width = destWidth;
        Height = destHeight;
        Resized?.Invoke(new RenderSurfaceAdapterResizedEventArgs(this, oldWidth, oldHeight, Width, Height));
    }

    /// <summary>
    /// Presents the specified portion of the Backbuffer image to the destination rectangle on the RenderSurfaceAdapter.
    /// </summary>
    /// <remarks>The method maps the specified region of the buffer image to the destination rectangle,
    /// scaling or transforming as necessary. Callers must ensure that the dimensions and coordinates of <paramref
    /// name="bufferRect"/> and <paramref name="destRect"/> are valid.</remarks>
    /// <param name="bufferImage">The source image from which to present. Cannot be <see langword="null"/>.</param>
    /// <param name="bufferRect">The rectangular region of the buffer image to present. Coordinates are in the buffer image's space.</param>
    /// <param name="destRect">The rectangular region in the destination space where the presented content will be drawn.</param>
    public abstract void Present(SKImage bufferImage, SKRectI bufferRect, SKRect destRect);
}