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

    private SKImage? _currentImage;
    private readonly Queue<SKImage> _toDispose = new();
    private SKRectI _sourceRect;

    //public SKColor ClearColor { get; set; } = SKColors.Black;

    private readonly SKPaint _presentPaint = new()
    {
        BlendMode = SKBlendMode.Src,
        FilterQuality = SKFilterQuality.None,
        IsAntialias = false
    };

    private readonly SKPaint _clearPaint = new()
    {
        BlendMode = SKBlendMode.Src,
        FilterQuality = SKFilterQuality.None,
        IsAntialias = false,
        Color = SKColors.Black
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="WinFormBitmapRenderSurfaceAdapter"/> class.
    /// </summary>
    /// <param name="control">The SKControl to use as the render target.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="control"/> is null.</exception>
    public WinFormBitmapRenderSurfaceAdapter(SKControl control)
        : base(control.ClientSize.Width, control.ClientSize.Height)
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
        SetDestinationSize(sz.Width, sz.Height);          // ← base will invoke Resized
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

        // Swap into current; old one queued for disposal after paint
        var old = _currentImage;
        _currentImage = bufferImage;
        if (!ReferenceEquals(old, _currentImage) && old is not null)
            _toDispose.Enqueue(old);

        _sourceRect = bufferRect;

        _control.Invalidate();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        var img = _currentImage;
        if (img == null) return;

        var srcI = SKRectI.Intersect(_sourceRect, new SKRectI(0, 0, img.Width, img.Height));
        if (srcI.IsEmpty) return;

        var destI = new SKRectI(srcI.Left, srcI.Top, srcI.Right, srcI.Bottom);

        var boundsI = new SKRectI(0, 0, e.Info.Width, e.Info.Height);
        var clippedDestI = SKRectI.Intersect(destI, boundsI);
        if (clippedDestI.IsEmpty) return;

        var dx = clippedDestI.Left - destI.Left;
        var dy = clippedDestI.Top - destI.Top;
        var clippedSrcI = new SKRectI(
            srcI.Left + dx, srcI.Top + dy,
            srcI.Left + dx + clippedDestI.Width,
            srcI.Top + dy + clippedDestI.Height);

        // clear the destination patch (overwrite)
        canvas.DrawRect(clippedDestI, _clearPaint);

        // blit the updated patch (overwrite)
        canvas.DrawImage(img, clippedSrcI, clippedDestI, _presentPaint);

        while (_toDispose.Count > 0)
            _toDispose.Dequeue().Dispose();
    }

    /// <summary>
    /// Releases all resources used by the <see cref="WinFormBitmapRenderSurfaceAdapter"/>.
    /// </summary>
    public void Dispose()
    {
        if (!_control.IsDisposed)
        {
            _control.PaintSurface -= OnPaintSurface;
            _control.HandleCreated -= OnHandleCreated;
            _control.ClientSizeChanged -= OnSizeChanged;
        }

        while (_toDispose.Count > 0)
            _toDispose.Dequeue().Dispose();

        _currentImage?.Dispose();
        _currentImage = null;
    }
}