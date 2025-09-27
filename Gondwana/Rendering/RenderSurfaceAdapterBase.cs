using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Gondwana.Rendering;

public abstract class RenderSurfaceAdapterBase
{
    public event EventHandler? Resized;

    public int Width { get; protected set; }
    public int Height { get; protected set; }

    protected RenderSurfaceAdapterBase(int destWidth, int destHeight)
    {
        SetDestinationSize(destWidth, destHeight);
    }

    protected void SetDestinationSize(int destWidth, int destHeight)
    {
        if (destWidth == Width && destHeight == Height) return;

        Engine.Logger.LogTrace("in SetDestinationSize() width: " + destWidth.ToString() + " height: " + destHeight.ToString());

        Width = destWidth;
        Height = destHeight;
        Resized?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Renders the specified portion of the Backbuffer image to the destination rectangle on the RenderSurfaceAdapter.
    /// </summary>
    /// <remarks>The method maps the specified region of the buffer image to the destination rectangle,
    /// scaling or transforming as necessary. Callers must ensure that the dimensions and coordinates of <paramref
    /// name="bufferRect"/> and <paramref name="destRect"/> are valid.</remarks>
    /// <param name="bufferImage">The source image to render from. Cannot be <see langword="null"/>.</param>
    /// <param name="bufferRect">The rectangular region of the buffer image to render. Coordinates are in the buffer image's space.</param>
    /// <param name="destRect">The rectangular region in the destination space where the rendered content will be drawn.</param>
    public abstract void Render(SKImage bufferImage, SKRectI bufferRect, SKRect destRect);
}