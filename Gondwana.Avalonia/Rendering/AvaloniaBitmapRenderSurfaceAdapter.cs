using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Gondwana.Rendering;
using SkiaSharp;

namespace Gondwana.Avalonia.Rendering;

/// <summary>
/// Provides a bitmap (CPU) render surface adapter for Avalonia that presents each frame by copying
/// pixel data from a <see cref="SKImage"/> into an <see cref="WriteableBitmap"/> displayed by
/// <see cref="AvaloniaBitmapRenderSurfaceControl"/>. This approach works on all Avalonia targets
/// (desktop, WebAssembly, Android, iOS, macOS) without requiring platform-specific SkiaSharp view packages.
/// </summary>
public class AvaloniaBitmapRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private readonly AvaloniaBitmapRenderSurfaceControl _control;

    // Owned by the UI thread: created/replaced in SetBitmap on the UI thread.
    private WriteableBitmap? _bitmap;

    // Current rendered image; swapped lock-free from any thread.
    private SKImage? _currentImage;
    private readonly Queue<SKImage> _toDispose = new();

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaBitmapRenderSurfaceAdapter"/> class.
    /// </summary>
    /// <param name="control">The <see cref="AvaloniaBitmapRenderSurfaceControl"/> to target.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is null.</exception>
    public AvaloniaBitmapRenderSurfaceAdapter(AvaloniaBitmapRenderSurfaceControl control)
        : base(Math.Max(1, (int)control.Bounds.Width), Math.Max(1, (int)control.Bounds.Height))
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _control.SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object? sender, global::Avalonia.Controls.SizeChangedEventArgs e)
    {
        RefreshDestinationSize();
    }

    /// <summary>
    /// Refreshes the destination size to match the control's current bounds.
    /// </summary>
    public void RefreshDestinationSize()
    {
        if (_disposed) return;
        var w = (int)_control.Bounds.Width;
        var h = (int)_control.Bounds.Height;
        if (w > 0 && h > 0)
            SetDestinationSize(w, h);
    }

    /// <summary>
    /// Presents the specified buffer image to the render surface by copying pixels into an
    /// Avalonia <see cref="WriteableBitmap"/> and scheduling a repaint on the UI thread.
    /// </summary>
    /// <param name="bufferImage">The image to present.</param>
    /// <param name="bufferRect">The source rectangle within the buffer image.</param>
    /// <param name="destRect">The destination rectangle on the render surface.</param>
    public override void Present(SKImage bufferImage, SKRectI bufferRect, SKRect destRect)
    {
        if (_disposed)
        {
            bufferImage.Dispose();
            return;
        }

        var old = _currentImage;
        _currentImage = bufferImage;
        if (!ReferenceEquals(old, _currentImage) && old is not null)
            _toDispose.Enqueue(old);

        // Schedule the pixel copy + repaint on the UI thread.
        Dispatcher.UIThread.Post(BlitAndInvalidate, DispatcherPriority.Render);
    }

    private void BlitAndInvalidate()
    {
        if (_disposed) return;

        var img = _currentImage;
        if (img == null) return;

        var w = img.Width;
        var h = img.Height;
        if (w <= 0 || h <= 0) return;

        // Recreate the writable bitmap only when dimensions change.
        if (_bitmap == null || _bitmap.PixelSize.Width != w || _bitmap.PixelSize.Height != h)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(w, h),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);
        }

        // Read pixels from SKImage (BGRA8888 premul) straight into the writable bitmap.
        using (var fb = _bitmap.Lock())
        {
            var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            img.ReadPixels(info, fb.Address, fb.RowBytes);
        }

        _control.SetBitmap(_bitmap);
        _control.InvalidateVisual();

        while (_toDispose.Count > 0)
            _toDispose.Dequeue().Dispose();
    }

    /// <summary>
    /// Releases all resources used by the <see cref="AvaloniaBitmapRenderSurfaceAdapter"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _control.SizeChanged -= OnSizeChanged;

        while (_toDispose.Count > 0)
            _toDispose.Dequeue().Dispose();

        _currentImage?.Dispose();
        _currentImage = null;

        _bitmap?.Dispose();
        _bitmap = null;
    }
}
