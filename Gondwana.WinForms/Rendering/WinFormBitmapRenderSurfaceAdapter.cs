using System.Collections.Concurrent;
using Gondwana.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Gondwana.WinForms.Rendering;

/// <summary>
/// Provides a render surface adapter for Windows Forms using SKControl and bitmap-based rendering.
/// </summary>
public class WinFormBitmapRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private readonly SKControl _control;
    private readonly object _presentSync = new();

    private SKImage? _currentImage;
    private readonly ConcurrentQueue<SKImage> _toDispose = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WinFormBitmapRenderSurfaceAdapter"/> class.
    /// </summary>
    /// <param name="control">The SKControl to use as the render target.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is null.</exception>
    public WinFormBitmapRenderSurfaceAdapter(SKControl control)
        : base(Math.Max(1, control.ClientSize.Width), Math.Max(1, control.ClientSize.Height), initialSizeAvailable: control.IsHandleCreated)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));

        _control.PaintSurface += OnPaintSurface;
        _control.HandleCreated += OnHandleCreated;
        _control.ClientSizeChanged += OnSizeChanged;

        // If the handle already exists, schedule one initial sync
        if (_control.IsHandleCreated)
            _control.BeginInvoke((Action)RefreshDestinationSize);
    }

    private void OnHandleCreated(object? s, EventArgs e) => RefreshDestinationSize();

    private void OnSizeChanged(object? s, EventArgs e) => RefreshDestinationSize();

    /// <summary>
    /// Refreshes the destination size based on the current client size of the control.
    /// </summary>
    public void RefreshDestinationSize()
    {
        if (_control.IsDisposed || !_control.IsHandleCreated) return;

        var sz = _control.ClientSize;                     // ← ClientSize, not Width/Height
        SetDestinationSize(sz.Width, sz.Height);
        _control.Invalidate();
    }

    /// <summary>
    /// Presents the specified buffer image to the render surface.
    /// </summary>
    /// <param name="bufferImage">The image to present.</param>
    /// <param name="bufferRect">The source rectangle within the buffer image.</param>
    /// <param name="destRect">The destination rectangle on the render surface.</param>
    public override void Present(SKImage bufferImage, SKRectI bufferRect, SKRect destRect)
    {
        // No UI target — dispose immediately to avoid leak
        if (_control.IsDisposed || !_control.IsHandleCreated)
        {
            bufferImage.Dispose();
            return;
        }

        var sourceRect = SKRectI.Intersect(
            bufferRect,
            new SKRectI(0, 0, bufferImage.Width, bufferImage.Height));

        var dirty = global::System.Drawing.Rectangle.FromLTRB(sourceRect.Left, sourceRect.Top, sourceRect.Right, sourceRect.Bottom);
        // Expand for the adjacent texels sampled by linear filtering, then round physical edges outwards.
        dirty.Inflate(1, 1);
        var invalidateRect = global::System.Drawing.Rectangle.Intersect(
            Presentation.ScreenRectToAdapterRect(dirty), _control.ClientRectangle);

        if (sourceRect == new SKRectI(0, 0, bufferImage.Width, bufferImage.Height))
            invalidateRect = _control.ClientRectangle;

        if (sourceRect.IsEmpty || invalidateRect.IsEmpty)
        {
            bufferImage.Dispose();
            return;
        }

        lock (_presentSync)
        {
            if (_disposed)
            {
                bufferImage.Dispose();
                return;
            }

            // Swap into current; old one is disposed after the paint that consumes the newest
            // complete snapshot. Windows coalesces physical invalidation rectangles.
            var old = _currentImage;
            _currentImage = bufferImage;
            if (!ReferenceEquals(old, _currentImage) && old is not null)
                _toDispose.Enqueue(old);

        }

        _control.Invalidate(invalidateRect);
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        SKImage? img;
        lock (_presentSync)
        {
            img = _currentImage;
        }

        try
        {
            if (img == null) return;

            // Retain the complete image for OS expose/resize paints with no new dirty patch.
            DrawImage(canvas, img, SKColors.Black);
        }
        finally
        {
            DisposeStaleImages();
        }
    }

    private void DisposeStaleImages()
    {
        while (_toDispose.TryDequeue(out var stale))
            stale.Dispose();
    }

    /// <summary>
    /// Releases all resources used by the <see cref="WinFormBitmapRenderSurfaceAdapter"/>.
    /// </summary>
    public void Dispose()
    {
        SKImage? currentImage;
        lock (_presentSync)
        {
            if (_disposed) return;
            _disposed = true;

            currentImage = _currentImage;
            _currentImage = null;
        }

        if (!_control.IsDisposed)
        {
            _control.PaintSurface -= OnPaintSurface;
            _control.HandleCreated -= OnHandleCreated;
            _control.ClientSizeChanged -= OnSizeChanged;
        }

        DisposeStaleImages();

        currentImage?.Dispose();
    }
}
