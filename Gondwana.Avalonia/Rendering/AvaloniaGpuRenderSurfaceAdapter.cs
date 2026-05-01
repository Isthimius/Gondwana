using Gondwana;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using SkiaSharp;

namespace Gondwana.Avalonia.Rendering;

/// <summary>
/// Provides a GPU-accelerated render surface adapter for Avalonia, backed by
/// <see cref="global::Avalonia.OpenGL.Controls.OpenGlControlBase"/>.
/// </summary>
/// <remarks>
/// <para>
/// The adapter's primary role is to track the physical-pixel dimensions of the render target
/// (consumed by <see cref="RenderSurfaceHost{T}"/> when computing the scene viewport) and to
/// post <c>RequestNextFrameRendering()</c> to the UI thread after each engine foreground cycle.
/// All actual GL operations — surface creation, rendering, and presentation — are handled by
/// <see cref="AvaloniaGpuRenderSurfaceControl"/>.
/// </para>
/// <para>
/// <strong>VSync:</strong> The <see cref="GpuBackbuffer.VSync"/> property has no effect when
/// used with this adapter; swap-interval control is delegated to Avalonia's compositor.
/// </para>
/// <para>
/// <strong>Platform support:</strong> Requires a GPU-capable Avalonia backend
/// (Windows/macOS/Linux desktop).  Not suitable for WebAssembly (WASM) targets; use
/// <see cref="AvaloniaBitmapRenderSurfaceAdapter"/> for cross-platform / WASM scenarios.
/// </para>
/// </remarks>
public sealed class AvaloniaGpuRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private Action? _afterFrameRenderHandler;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the color used to clear the surface before rendering.
    /// </summary>
    public SKColor ClearColor { get; set; } = SKColors.Black;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaGpuRenderSurfaceAdapter"/> class.
    /// </summary>
    /// <param name="initialWidth">Initial width in physical pixels.</param>
    /// <param name="initialHeight">Initial height in physical pixels.</param>
    internal AvaloniaGpuRenderSurfaceAdapter(int initialWidth, int initialHeight)
        : base(Math.Max(1, initialWidth), Math.Max(1, initialHeight))
    {
    }

    /// <summary>
    /// Updates the adapter's reported physical-pixel dimensions so that the
    /// <see cref="RenderSurfaceHost{T}"/> computes the correct scene viewport.
    /// Called from <see cref="AvaloniaGpuRenderSurfaceControl"/> on each
    /// <c>OnOpenGlRender</c> before scene rendering begins.
    /// </summary>
    internal void UpdateDimensions(int physW, int physH)
    {
        if (!_disposed)
            SetDestinationSize(Math.Max(1, physW), Math.Max(1, physH));
    }

    /// <summary>
    /// Wires the GL repaint callback to the engine's foreground cycle.
    /// After this call, every <c>Engine.AfterFrameRender</c> event posts
    /// <paramref name="requestRepaint"/> to the UI thread.
    /// </summary>
    internal void AttachToEngine(Action requestRepaint)
    {
        _afterFrameRenderHandler = () =>
        {
            if (!_disposed)
                Engine.Instance.UiDispatcher!.Post(requestRepaint);
        };
        Engine.Instance.AfterFrameRender += _afterFrameRenderHandler;
    }

    /// <summary>
    /// Not used in the GL-thread path.  The image is disposed immediately to avoid a leak.
    /// </summary>
    public override void Present(SKImage bufferImage, SKRectI bufferRect, SKRect destRect)
    {
        bufferImage.Dispose();
    }

    /// <summary>
    /// Releases all resources used by the <see cref="AvaloniaGpuRenderSurfaceAdapter"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_afterFrameRenderHandler != null)
        {
            Engine.Instance.AfterFrameRender -= _afterFrameRenderHandler;
            _afterFrameRenderHandler = null;
        }
    }
}
