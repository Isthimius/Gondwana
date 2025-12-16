using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Rendering;

/// <summary>
/// Represents a base class for hosting a render surface, providing functionality for managing rendering operations,
/// backbuffer access, and integration with platform-specific adapters.
/// </summary>
public abstract class RenderSurfaceHostBase : IDisposable
{
    protected Rectangle _overlayScreenDirty = Rectangle.Empty;

    protected RenderSurfaceHostBase() => RenderSurfaceHostRegistry.Register(this);

    ~RenderSurfaceHostBase() => Dispose(false);

    /// <summary>
    /// Gets the in-memory <see cref="BackbufferBase"/> associated with the current rendering context.
    /// </summary>
    public abstract BackbufferBase? Backbuffer { get; }

    /// <summary>
    /// Gets the source <see cref="Scenes.Scene"/> used for rendering operations.
    /// </summary>
    public abstract Scene? Scene { get; }

    /// <summary>
    /// Gets the platform-specific <see cref="RenderSurfaceAdapterBase"> responsible
    /// for rendering the image from the <see cref="Backbuffer"/>.
    /// </summary>
    public abstract RenderSurfaceAdapterBase? RenderSurfaceAdapter { get; }

    public abstract ViewRenderer? ViewRenderer { get; }

    /// <summary>
    /// Runs as part of DoBackgroundTasks(). Takes content of RefreshQueue
    /// - which is a queue of tiles that need to be (re)drawn -
    /// and draws them to the backbuffer. This, in turn, updates the
    /// Backbuffer.DirtyRectangle.
    /// </summary>
    internal abstract void DrawRefreshQueueToBackbuffer(long tick);

    /// <summary>
    /// Runs as part of DoForegroundTasks(). This renders the DirtyRectangle
    /// area of the backbuffer to the adapter.
    /// </summary>
    internal abstract void RenderBackbufferToAdapter();

    /// <summary>
    /// Marks a specified rectangular region of the overlay screen as dirty, called from DirectDrawing instances.
    /// </summary>
    /// <remarks>If the specified rectangle is empty, the method performs no action. Subsequent calls will
    /// expand the dirty region to include the union of the previously marked region and the new rectangle.</remarks>
    /// <param name="screenRect">The <see cref="Rectangle"/> representing the region to mark as dirty.
    /// ***** Note: this is SCREEN PIXELS ***** </param>
    protected internal void AddOverlayScreenDirty(Rectangle screenRect)
    {
        if (screenRect.IsEmpty)
            return;

        _overlayScreenDirty = _overlayScreenDirty.IsEmpty
            ? screenRect
            : Rectangle.Union(_overlayScreenDirty, screenRect);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) => RenderSurfaceHostRegistry.Unregister(this);
}
