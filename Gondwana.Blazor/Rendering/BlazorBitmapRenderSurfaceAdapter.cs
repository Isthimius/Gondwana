using System.Runtime.InteropServices;
using Gondwana.Rendering;
using SkiaSharp;

namespace Gondwana.Blazor.Rendering;

/// <summary>
/// Provides a bitmap (CPU) render surface adapter for Blazor that presents each frame by
/// converting pixel data from a <see cref="SKImage"/> into an RGBA byte array and drawing it
/// onto the HTML canvas element owned by <see cref="BlazorBitmapRenderSurfaceComponent"/>.
/// </summary>
/// <remarks>
/// This approach works in Blazor WebAssembly without requiring platform-specific SkiaSharp view
/// packages. Each frame the engine produces is read into a managed RGBA byte array and then
/// transferred to the browser canvas via the <c>gondwana-blazor.js</c> JS module.
/// </remarks>
public sealed class BlazorBitmapRenderSurfaceAdapter : RenderSurfaceAdapterBase, IDisposable
{
    private readonly BlazorBitmapRenderSurfaceComponent _component;
    private byte[]? _pixelBuffer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="BlazorBitmapRenderSurfaceAdapter"/>.
    /// </summary>
    /// <param name="component">The Blazor component that owns the canvas element.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="component"/> is null.</exception>
    public BlazorBitmapRenderSurfaceAdapter(BlazorBitmapRenderSurfaceComponent component)
        : base(1, 1)
    {
        _component = component ?? throw new ArgumentNullException(nameof(component));
    }

    /// <summary>
    /// Updates the reported surface size. Called by the component after querying canvas client dimensions.
    /// </summary>
    /// <param name="width">New width in pixels.</param>
    /// <param name="height">New height in pixels.</param>
    internal void UpdateSize(int width, int height)
    {
        SetDestinationSize(width, height);
    }

    /// <summary>
    /// Presents the specified buffer image to the render surface by reading pixels into an RGBA
    /// byte array and scheduling a canvas update on the Blazor component's UI context.
    /// </summary>
    /// <param name="bufferImage">The source image to present.</param>
    /// <param name="bufferRect">The source rectangle within the buffer image.</param>
    /// <param name="destRect">The destination rectangle on the render surface.</param>
    public override void Present(SKImage bufferImage, SKRectI bufferRect, SKRect destRect)
    {
        if (_disposed)
        {
            bufferImage.Dispose();
            return;
        }

        var srcRect = SKRectI.Intersect(bufferRect, new SKRectI(0, 0, bufferImage.Width, bufferImage.Height));
        var w = srcRect.Width;
        var h = srcRect.Height;
        if (w <= 0 || h <= 0)
        {
            bufferImage.Dispose();
            return;
        }

        var needed = w * h * 4;
        if (_pixelBuffer == null || _pixelBuffer.Length != needed)
            _pixelBuffer = new byte[needed];

        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        // Pin the managed array so we can pass its address to native SkiaSharp code.
        var gcHandle = GCHandle.Alloc(_pixelBuffer, GCHandleType.Pinned);
        bool success;
        try
        {
            success = bufferImage.ReadPixels(info, gcHandle.AddrOfPinnedObject(), w * 4, srcRect.Left, srcRect.Top);
        }
        finally
        {
            gcHandle.Free();
        }

        if (!success)
        {
            bufferImage.Dispose();
            return;
        }

        // In Blazor WASM (single-threaded) no copy is needed: the frame cannot be overwritten
        // before the queued InvokeAsync action consumes it. For Blazor Server the host is
        // expected to configure a separate loop, but the copy is omitted here for performance.
        _component.EnqueueFrame(_pixelBuffer, w, h, srcRect.Left, srcRect.Top, bufferImage.Width, bufferImage.Height);

        bufferImage.Dispose();
    }

    /// <summary>Releases all resources used by the adapter.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pixelBuffer = null;
    }
}
