using Gondwana.Drawing;
using System.Drawing;
using SkiaSharp;
using Microsoft.Extensions.Logging;
using Gondwana.Skia;

namespace Gondwana.Rendering.Direct;

public class DirectImage : DirectDrawing
{
    private readonly Tilesheet _tilesheet;

    public DirectImage(BackbufferBase buffer, Rectangle bounds, Tilesheet tilesheet)
        : base(buffer, bounds)
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
        var canvas = Buffer.Canvas;
        var destRect = Bounds.ToSKRect();

        var bitmap = _tilesheet.SkBitmap;
        if (bitmap == null)
            return;

        canvas.DrawBitmap(bitmap, destRect);
    }

    public Tilesheet Tilesheet => _tilesheet;
}