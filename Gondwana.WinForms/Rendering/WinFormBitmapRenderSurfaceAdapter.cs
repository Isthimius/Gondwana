using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Gondwana.Rendering;

namespace Gondwana.WinForms.Rendering;

public class WinFormBitmapRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private readonly SKControl _control;
    private SKImage? _currentImage;
    private SKRectI _sourceRect;
    private SKRect _destRect;

    public WinFormBitmapRenderSurfaceAdapter(SKControl control)
        : base(control.Width, control.Height)
    {
        _control = control;
        _control.PaintSurface += OnPaintSurface;
        _control.SizeChanged += (_, _) => SetDestinationSize(_control.Width, _control.Height);
    }

    public SKColor ClearColor { get; set; } = SKColors.Black;

    public override void Render(SKImage bufferImage, SKRectI bufferRect, SKRect destRect)
    {
        // Cache references
        _currentImage = bufferImage;
        _sourceRect = bufferRect;
        _destRect = destRect;

        // Trigger a redraw
        _control.Invalidate();
    }

    //private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    //{
    //    if (_currentImage == null)
    //        return;

    //    var canvas = e.Surface.Canvas;
    //    canvas.Clear(ClearColor);
    //    canvas.DrawImage(_currentImage, _sourceRect, _destRect);
    //}

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.DarkSlateGray); // Should always appear

        if (_currentImage != null)
        {
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawImage(_currentImage, _sourceRect, _destRect);
            canvas.DrawRect(_destRect, paint); // Outline to visualize target area
        }
    }

    public void Dispose()
    {
        _control.PaintSurface -= OnPaintSurface;
        _currentImage = null;
    }
}
