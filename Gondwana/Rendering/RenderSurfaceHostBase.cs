using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Rendering;

/// <summary>
/// Represents a base class for hosting a render surface, providing functionality for managing rendering operations,
/// backbuffer access, and integration with platform-specific adapters.
/// </summary>
/// <remarks>This abstract class serves as the foundation for implementing render surface hosts. It provides
/// properties for accessing the backbuffer, the source scene to be drawn, the clear color used for rendering, and the
/// associated render surface adapter. Derived classes must implement the abstract members to define specific rendering
/// behavior. The class also manages lifecycle operations, including registration with the render surface host
/// registry and cleanup during disposal.</remarks>
public abstract class RenderSurfaceHostBase : IDisposable
{
    protected RenderSurfaceHostBase() => RenderSurfaceHostRegistry.Register(this);

    ~RenderSurfaceHostBase() => Dispose(false);

    /// <summary>
    /// Gets the in-memory <see cref="BackbufferBase"/> associated with the current rendering context.
    /// </summary>
    public abstract BackbufferBase Backbuffer { get; }

    /// <summary>
    /// Gets the source <see cref="Scene"/> used for rendering operations.
    /// </summary>
    public abstract Scene? DrawSource { get; }

    /// <summary>
    /// Gets the color used when filling area of the backbuffer that is not covered by any tiles.
    /// </summary>
    public abstract Color ClearColor { get; }

    /// <summary>
    /// Gets the platform-specific <see cref="RenderSurfaceAdapterBase"> responsible
    /// for rendering the image from the <see cref="Backbuffer"/>.
    /// </summary>
    public abstract RenderSurfaceAdapterBase? RenderSurfaceAdapter { get; }

    /// <summary>
    /// Runs as part of DoBackgroundTasks(). Takes content of RefreshQueue
    /// - which is a queue of tiles that need to be redrawn -
    /// and draws them to the backbuffer. This, in turn, updates the
    /// Backbuffer.DirtyRectangle.
    /// </summary>
    internal abstract void DrawRefreshQueueToBackbuffer();

    /// <summary>
    /// Runs as part of DoForegroundTasks(). This renders the DirtyRectangle
    /// area of the backbuffer to the adapter.
    /// </summary>
    internal abstract void RenderBackbufferToAdapter();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        RenderSurfaceHostRegistry.Unregister(this);
    }
}