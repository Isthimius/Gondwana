using Gondwana.Rendering;
using SkiaSharp;

namespace Gondwana.Blazor.Rendering;

/// <summary>
/// Coordinates engine frame requests with a Blazor WebGL render surface.
/// </summary>
/// <remarks>
/// Presentation is performed directly inside the <c>SKGLView</c> paint callback. The adapter
/// therefore does not consume bitmap snapshots through <see cref="Present"/>; it coalesces engine
/// repaint requests and tracks the logical canvas size instead.
/// </remarks>
public sealed class BlazorGpuRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private readonly Action _requestRender;
    private Action? _afterFrameRenderHandler;
    private int _pendingInvalidate;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="BlazorGpuRenderSurfaceAdapter"/>.
    /// </summary>
    /// <param name="requestRender">Requests an <c>SKGLView</c> paint callback.</param>
    internal BlazorGpuRenderSurfaceAdapter(Action requestRender)
        : base(1, 1)
    {
        _requestRender = requestRender ?? throw new ArgumentNullException(nameof(requestRender));
    }

    /// <summary>
    /// Subscribes to the engine foreground cadence so each completed foreground update requests
    /// one WebGL frame.
    /// </summary>
    internal void AttachToEngine()
    {
        if (_afterFrameRenderHandler is not null)
            return;

        _afterFrameRenderHandler = () =>
        {
            if (_disposed || Interlocked.CompareExchange(ref _pendingInvalidate, 1, 0) != 0)
                return;

            var dispatcher = Engine.Instance.UiDispatcher;
            if (dispatcher is null)
            {
                Interlocked.Exchange(ref _pendingInvalidate, 0);
                return;
            }

            dispatcher.Post(() =>
            {
                if (_disposed)
                {
                    Interlocked.Exchange(ref _pendingInvalidate, 0);
                    return;
                }

                _requestRender();
            });
        };

        Engine.Instance.AfterFrameRender += _afterFrameRenderHandler;
    }

    /// <summary>
    /// Marks a requested frame as delivered and updates the adapter's logical canvas dimensions.
    /// </summary>
    internal void BeginPaint(int width, int height)
    {
        Interlocked.Exchange(ref _pendingInvalidate, 0);

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
        Interlocked.Exchange(ref _pendingInvalidate, 0);

        if (_afterFrameRenderHandler is not null)
        {
            Engine.Instance.AfterFrameRender -= _afterFrameRenderHandler;
            _afterFrameRenderHandler = null;
        }
    }
}
