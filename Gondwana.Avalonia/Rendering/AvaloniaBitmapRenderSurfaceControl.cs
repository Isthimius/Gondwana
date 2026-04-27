using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;

namespace Gondwana.Avalonia.Rendering;

/// <summary>
/// An Avalonia <see cref="Control"/> that displays game frames produced by the Gondwana engine.
/// Frames are received as <see cref="WriteableBitmap"/> instances from the
/// <see cref="AvaloniaBitmapRenderSurfaceAdapter"/> and rendered via the Avalonia compositor.
/// No platform-specific SkiaSharp view package is required; this works on all Avalonia targets.
/// </summary>
public class AvaloniaBitmapRenderSurfaceControl : Control
{
    private WriteableBitmap? _bitmap;

    /// <summary>
    /// Gets the render surface adapter used to drive this control.
    /// </summary>
    public AvaloniaBitmapRenderSurfaceAdapter Adapter { get; }

    /// <summary>
    /// Gets the <see cref="RenderSurfaceHost{T}"/> bound to this control.
    /// </summary>
    public RenderSurfaceHost<BitmapBackbuffer> Host { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaBitmapRenderSurfaceControl"/> class.
    /// </summary>
    public AvaloniaBitmapRenderSurfaceControl()
    {
        Adapter = new AvaloniaBitmapRenderSurfaceAdapter(this);
        Host = new RenderSurfaceHost<BitmapBackbuffer>(Adapter);

        SizeChanged += (_, _) => Adapter.RefreshDestinationSize();
    }

    /// <summary>
    /// Updates the bitmap to display on the next render pass.
    /// Must be called on the UI thread (called internally by the adapter).
    /// </summary>
    /// <param name="bitmap">The new bitmap to display.</param>
    internal void SetBitmap(WriteableBitmap bitmap) => _bitmap = bitmap;

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        var bmp = _bitmap;
        if (bmp != null)
            context.DrawImage(bmp, new Rect(bmp.Size));
    }
}
