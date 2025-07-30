using SkiaSharp.Views.Desktop;
using SkiaSharp;
using Gondwana.Rendering;

namespace Gondwana.WinForms.Rendering;

public class WinFormGpuRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private readonly SKGLControl _glControl;
    private SKImage? _currentImage;
    private SKRectI _sourceRect;
    private SKRect _destRect;

    public WinFormGpuRenderSurfaceAdapter(SKGLControl glControl)
        : base(glControl.Width, glControl.Height)
    {
        _glControl = glControl;
        _glControl.PaintSurface += OnPaintSurface;
        _glControl.SizeChanged += (_, _) => SetDestinationSize(_glControl.Width, _glControl.Height);
    }

    public SKColor ClearColor { get; set; } = SKColors.Black;

    public override void Render(SKImage bufferImage, SKRectI bufferRect, SKRect destRect)
    {
        _currentImage = bufferImage;
        _sourceRect = bufferRect;
        _destRect = destRect;

        _glControl.Invalidate();
    }

    private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        if (_currentImage == null)
            return;

        var canvas = e.Surface.Canvas;
        canvas.Clear(ClearColor);
        canvas.DrawImage(_currentImage, _sourceRect, _destRect);
    }

    public void Dispose()
    {
        _glControl.PaintSurface -= OnPaintSurface;
        _currentImage = null;
    }
}
