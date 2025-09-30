using System.Drawing;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Rendering;
using Gondwana.Skia;
using Microsoft.Extensions.Logging;

namespace Gondwana.Drawing.Direct;

public class DirectImage : DirectDrawingBase
{
    private readonly Tilesheet _tilesheet;

    public DirectImage(RenderSurfaceHostBase renderSurfaceHost, Rectangle bounds, Tilesheet tilesheet)
        : base(renderSurfaceHost, bounds)
    {
        if (tilesheet == null)
        {
            Engine.Logger.LogError("Tilesheet was null when creating DirectImage.");
            throw new ArgumentNullException(nameof(tilesheet));
        }

        _tilesheet = tilesheet;
    }

    protected internal override void Render()
    {
        var canvas = RenderSurfaceHost.Backbuffer.Canvas;
        var destRect = Bounds.ToSKRect();

        var bitmap = _tilesheet.SkBitmap;
        if (bitmap == null)
            return;

        canvas.DrawBitmap(bitmap, destRect);
    }

    public Tilesheet Tilesheet => _tilesheet;
}