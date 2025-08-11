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
        Width = destWidth;
        Height = destHeight;
        Resized?.Invoke(this, EventArgs.Empty);
    }

    public abstract void Render(SKImage bufferImage, SKRectI bufferRect, SKRect destRect);
}
