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

    internal void Render(SKImage bufferImage, SKRectI dirtyRect)
    {
        float scaleX = (float)Width / bufferImage.Width;
        float scaleY = (float)Height / bufferImage.Height;

        var destRect = new SKRect(
            dirtyRect.Left * scaleX,
            dirtyRect.Top * scaleY,
            dirtyRect.Right * scaleX,
            dirtyRect.Bottom * scaleY
        );

        Render(bufferImage, dirtyRect, destRect);
    }

    public abstract void Render(SKImage bufferImage, SKRectI bufferRect, SKRect destRect);
}
