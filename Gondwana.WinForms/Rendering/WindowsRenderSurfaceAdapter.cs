using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Gondwana.Rendering
{
    public class WindowsRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
    {
        private readonly SKControl _control;
        private SKImage? _currentImage;
        private SKRectI _sourceRect;
        private SKRect _destRect;

        public WindowsRenderSurfaceAdapter(SKControl control)
            : base(control.Width, control.Height)
        {
            _control = control;
            _control.PaintSurface += OnPaintSurface;

            _control.SizeChanged += (_, _) =>
            {
                SetDestinationSize(_control.Width, _control.Height);
            };
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

        private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            if (_currentImage == null)
                return;

            var canvas = e.Surface.Canvas;
            canvas.Clear(ClearColor);

            // Draw only the specified region
            canvas.DrawImage(_currentImage, _sourceRect, _destRect);
        }

        public void Dispose()
        {
            _control.PaintSurface -= OnPaintSurface;
        }
    }
}
