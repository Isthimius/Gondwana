using System.Collections.Concurrent;
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
    private readonly object _presentSync = new();

    // Owned by the UI thread: created/replaced in SetBitmap on the UI thread.
    private WriteableBitmap? _bitmap;

    // Current rendered image and accumulated dirty area; guarded by _presentSync.
    private SKImage? _currentImage;
    private SKRectI _pendingSourceRect;
    private bool _blitScheduled;

    // Images queued for disposal; written from any thread (Present), drained on UI thread.
    private readonly ConcurrentQueue<SKImage> _toDispose = new();

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
        var sourceRect = SKRectI.Intersect(
            bufferRect,
            new SKRectI(0, 0, bufferImage.Width, bufferImage.Height));

        if (sourceRect.IsEmpty)
        {
            bufferImage.Dispose();
            return;
        }

        bool scheduleBlit;
        lock (_presentSync)
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

            _pendingSourceRect = Union(_pendingSourceRect, sourceRect);
            scheduleBlit = !_blitScheduled;
            _blitScheduled = true;
        }

        // Several presents may arrive before the UI thread runs. One scheduled blit copies the
        // union of their dirty regions from the newest (complete) backbuffer snapshot.
        if (scheduleBlit)
            Dispatcher.UIThread.Post(BlitAndInvalidate, DispatcherPriority.Render);
    }

    private void BlitAndInvalidate()
    {
        SKImage? img;
        SKRectI sourceRect;

        lock (_presentSync)
        {
            if (_disposed)
            {
                _blitScheduled = false;
                return;
            }

            img = _currentImage;
            sourceRect = _pendingSourceRect;
            _pendingSourceRect = SKRectI.Empty;
            _blitScheduled = false;
        }

        if (img == null) return;

        try
        {
            var w = img.Width;
            var h = img.Height;
            if (w <= 0 || h <= 0) return;

            var imageBounds = new SKRectI(0, 0, w, h);
            sourceRect = SKRectI.Intersect(sourceRect, imageBounds);
            if (sourceRect.IsEmpty) return;

            // A new bitmap has no retained pixels, so its first copy must cover the full image.
            var bitmap = _bitmap;
            if (bitmap == null || bitmap.PixelSize.Width != w || bitmap.PixelSize.Height != h)
            {
                bitmap?.Dispose();
                bitmap = new WriteableBitmap(
                    new PixelSize(w, h),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul);

                _bitmap = bitmap;
                sourceRect = imageBounds;
            }

            // Copy only the accumulated dirty patch into its matching location in the bitmap.
            bool copied;
            using (var fb = bitmap.Lock())
            {
                var info = new SKImageInfo(
                    sourceRect.Width,
                    sourceRect.Height,
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul);

                var destinationOffset = checked((sourceRect.Top * fb.RowBytes) + (sourceRect.Left * 4));
                var destination = IntPtr.Add(fb.Address, destinationOffset);

                copied = img.ReadPixels(
                    info,
                    destination,
                    fb.RowBytes,
                    sourceRect.Left,
                    sourceRect.Top);
            }

            if (!copied) return;

            _control.SetBitmap(bitmap);
            _control.InvalidateVisual();
        }
        finally
        {
            DisposeStaleImages();
        }
    }

    private static SKRectI Union(SKRectI left, SKRectI right)
    {
        if (left.IsEmpty) return right;
        if (right.IsEmpty) return left;

        return new SKRectI(
            Math.Min(left.Left, right.Left),
            Math.Min(left.Top, right.Top),
            Math.Max(left.Right, right.Right),
            Math.Max(left.Bottom, right.Bottom));
    }

    private void DisposeStaleImages()
    {
        while (_toDispose.TryDequeue(out var stale))
            stale.Dispose();
    }

    /// <summary>
    /// Releases all resources used by the <see cref="AvaloniaBitmapRenderSurfaceAdapter"/>.
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
            _pendingSourceRect = SKRectI.Empty;
            _blitScheduled = false;
        }

        _control.SizeChanged -= OnSizeChanged;

        DisposeStaleImages();

        currentImage?.Dispose();

        _bitmap?.Dispose();
        _bitmap = null;
    }
}
