using SkiaSharp;

namespace Gondwana.Rendering;

public abstract class RenderSurfaceAdapterBase
{
    public event Action<RenderSurfaceAdapterResizedEventArgs>? Resized;

    public int Width { get; protected set; }
    public int Height { get; protected set; }

    protected RenderSurfaceAdapterBase(int destWidth, int destHeight)
    {
        SetDestinationSize(destWidth, destHeight);
    }

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