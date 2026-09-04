using Gondwana.Rendering;
using SkiaSharp;

namespace Gondwana.Blazor.Rendering;

/// <summary>
/// Coordinates Gondwana foreground-frame requests with a Blazor WebGL render surface.
/// </summary>
/// <remarks>
/// The <c>SKGLView</c> owns the browser animation loop. This adapter therefore does not request
/// another browser frame when the engine completes a foreground update; it only records that a
/// new scene frame is due. The active <c>SKGLView</c> paint callback consumes that request and
/// renders the scene immediately while the WebGL context is current.
/// </remarks>
public sealed class BlazorGpuRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private Action? _afterFrameRenderHandler;
    private int _pendingFrame;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="BlazorGpuRenderSurfaceAdapter"/>.
    /// </summary>
    internal BlazorGpuRenderSurfaceAdapter()
        : base(1, 1)
    {
    }

    /// <summary>
    /// Subscribes to the engine foreground cadence and records when a new WebGL scene frame is due.
    /// </summary>
    internal void AttachToEngine()
    {
        if (_afterFrameRenderHandler is not null)
            return;

        _afterFrameRenderHandler = () =>
        {
            if (!_disposed)
                Interlocked.Exchange(ref _pendingFrame, 1);
        };

        Engine.Instance.AfterFrameRender += _afterFrameRenderHandler;
    }

    /// <summary>
    /// Returns whether the engine has requested a new scene frame since the previous consume.
    /// </summary>
    internal bool ConsumeFrameRequest() =>
        !_disposed && Interlocked.Exchange(ref _pendingFrame, 0) != 0;

    /// <summary>
    /// Updates the adapter's logical canvas dimensions for the active WebGL paint callback.
    /// </summary>
    internal void BeginPaint(int width, int height)
    {
        if (!_disposed && width > 0 && height > 0)
            SetDestinationSize(width, height);
    }

    /// <summary>
    /// Not used by the WebGL path. GPU rendering and presentation both occur in the
    /// <c>SKGLView</c> paint callback.
    /// </summary>
    public override void Present(SKImage bufferImage, SKRectI bufferRect, SKRect destRect)
    {
        bufferImage.Dispose();
    }

    /// <summary>Releases the engine frame-request subscription.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Interlocked.Exchange(ref _pendingFrame, 0);

        if (_afterFrameRenderHandler is not null)
        {
            Engine.Instance.AfterFrameRender -= _afterFrameRenderHandler;
            _afterFrameRenderHandler = null;
        }
    }
}
