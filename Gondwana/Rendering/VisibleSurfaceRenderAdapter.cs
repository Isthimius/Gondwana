using SkiaSharp;

namespace Gondwana.Rendering;

public abstract class VisibleSurfaceRenderAdapter
{
    public int DestWidth { get; protected set; }
    public int DestHeight { get; protected set; }

    protected VisibleSurfaceRenderAdapter(int destWidth, int destHeight)
    {
        SetDestinationSize(destWidth, destHeight);
    }

    protected void SetDestinationSize(int destWidth, int destHeight)
    {
        DestWidth = destWidth;
        DestHeight = destHeight;
    }

    internal void Render(SKImage bufferImage, SKRectI dirtyRect)
    {
        float scaleX = (float)DestWidth / bufferImage.Width;
        float scaleY = (float)DestHeight / bufferImage.Height;

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
