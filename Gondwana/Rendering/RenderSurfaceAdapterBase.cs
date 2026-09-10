using SkiaSharp;
using System.Drawing;

namespace Gondwana.Rendering;

/// <summary>
/// Represents the abstract base class for render surface adapters that present backbuffer output
/// to a destination surface.
/// </summary>
public abstract class RenderSurfaceAdapterBase
{
    private sealed class PresentationState(int bufferWidth, int bufferHeight, int adapterWidth, int adapterHeight)
    {
        internal readonly int BufferWidth = bufferWidth, BufferHeight = bufferHeight;
        internal readonly int AdapterWidth = adapterWidth, AdapterHeight = adapterHeight;
        internal readonly PresentationTransform Transform = PresentationTransform.Fit(bufferWidth, bufferHeight, adapterWidth, adapterHeight);
    }

    private readonly object _presentationSync = new();
    private PresentationState _presentation = new(1, 1, 0, 0);
    internal bool InitialSizeAvailable { get; private set; }

    /// <summary>Current immutable aspect-preserving mapping of Backbuffer ScreenPx to adapter pixels.</summary>
    public PresentationTransform Presentation => Volatile.Read(ref _presentation).Transform;

    /// <summary>Current derived presentation scale; adapter resize does not resize the Backbuffer.</summary>
    public float PresentationScale => Presentation.Scale;

    internal void SetBackbufferSize(int width, int height)
    {
        lock (_presentationSync)
        {
            var state = _presentation;
            Volatile.Write(ref _presentation, new(width, height, state.AdapterWidth, state.AdapterHeight));
        }
    }

    /// <summary>Converts adapter input to logical ScreenPx, retaining outside coordinates for capture/leave.</summary>
    public Point AdapterPxToScreenPx(PointF adapterPx)
    {
        Presentation.TryAdapterPxToScreenPx(adapterPx, out var screen);
        // Floor is essential: a fractional negative margin must not truncate to the edge pixel zero.
        return new Point((int)Math.Floor(screen.X), (int)Math.Floor(screen.Y));
    }

    /// <summary>Presents a complete image using the same transform as pointer normalization.</summary>
    public void DrawImage(SKCanvas canvas, SKImage image, SKColor clearColor)
    {
        var state = Volatile.Read(ref _presentation);
        // A queued CPU snapshot may precede an explicit resolution change. Fit that complete
        // image using the same authoritative calculation until its replacement arrives.
        var transform = image.Width == state.BufferWidth && image.Height == state.BufferHeight
            ? state.Transform : PresentationTransform.Fit(image.Width, image.Height, state.AdapterWidth, state.AdapterHeight);
        canvas.Clear(clearColor);
        if (transform.Scale <= 0) return;
        canvas.DrawImage(image, transform.DestinationRect, PresentationSampling);
    }

    internal static SKSamplingOptions PresentationSampling => new(
        Engine.Instance.Configuration.RenderScalingFilter == RenderScalingFilter.NearestNeighbor
            ? SKFilterMode.Nearest : SKFilterMode.Linear);

    /// <summary>
    /// Occurs when the render surface adapter is resized.
    /// </summary>
    /// <remarks>
    /// This event is raised when the <see cref="Width"/> or <see cref="Height"/> properties change,
    /// providing both the old and new dimensions in the event arguments.
    /// </remarks>
    public event Action<RenderSurfaceAdapterResizedEventArgs>? Resized;

    /// <summary>
    /// Gets the current width of the render surface in pixels.
    /// </summary>
    /// <value>The width of the render surface.</value>
    public int Width
    {
        get => Volatile.Read(ref _presentation).AdapterWidth;
        protected set => SetDestinationSize(value, Height);
    }

    /// <summary>
    /// Gets the current height of the render surface in pixels.
    /// </summary>
    /// <value>The height of the render surface.</value>
    public int Height
    {
        get => Volatile.Read(ref _presentation).AdapterHeight;
        protected set => SetDestinationSize(Width, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderSurfaceAdapterBase"/> class with the specified dimensions.
    /// </summary>
    /// <param name="destWidth">The initial width of the render surface in pixels.</param>
    /// <param name="destHeight">The initial height of the render surface in pixels.</param>
    protected RenderSurfaceAdapterBase(int destWidth, int destHeight)
        : this(destWidth, destHeight, initialSizeAvailable: true) { }

    /// <summary>Constructs an adapter whose initial layout may still be pending.</summary>
    /// <param name="destWidth">Initial or placeholder adapter width.</param>
    /// <param name="destHeight">Initial or placeholder adapter height.</param>
    /// <param name="initialSizeAvailable">False for hosts constructed before layout; the first valid size establishes resolution once.</param>
    protected RenderSurfaceAdapterBase(int destWidth, int destHeight, bool initialSizeAvailable)
    {
        SetDestinationSize(destWidth, destHeight);
        InitialSizeAvailable = initialSizeAvailable;
    }

    /// <summary>
    /// Sets the destination size of the render surface and raises the <see cref="Resized"/> event if the dimensions have changed.
    /// </summary>
    /// <param name="destWidth">The new width of the render surface in pixels.</param>
    /// <param name="destHeight">The new height of the render surface in pixels.</param>
    /// <remarks>
    /// If the specified dimensions are the same as the current dimensions, this method returns without making changes
    /// or raising the <see cref="Resized"/> event.
    /// </remarks>
    protected void SetDestinationSize(int destWidth, int destHeight)
    {
        if (destWidth == Width && destHeight == Height && InitialSizeAvailable)
            return;

        var oldWidth = Width;
        var oldHeight = Height;

        InitialSizeAvailable = destWidth > 0 && destHeight > 0;
        lock (_presentationSync)
        {
            var state = _presentation;
            Volatile.Write(ref _presentation, new(state.BufferWidth, state.BufferHeight, destWidth, destHeight));
        }
        Resized?.Invoke(new RenderSurfaceAdapterResizedEventArgs(this, oldWidth, oldHeight, Width, Height));
    }

    /// <summary>
    /// Presents the specified portion of the Backbuffer image to the destination rectangle on the RenderSurfaceAdapter.
    /// </summary>
    /// <remarks>The method maps the specified region of the buffer image to the destination rectangle,
    /// scaling or transforming as necessary. Callers must ensure that the dimensions and coordinates of <paramref
    /// name="bufferRect"/> and <paramref name="destRect"/> are valid.</remarks>
    /// <param name="bufferImage">The source image from which to present. Cannot be <see langword="null"/>.</param>
    /// <param name="bufferRect">The rectangular region of the buffer image to present. Coordinates are in the buffer image's space.</param>
    /// <param name="destRect">The rectangular region in the destination space where the presented content will be drawn.</param>
    public abstract void Present(SKImage bufferImage, SKRectI bufferRect, SKRect destRect);
}