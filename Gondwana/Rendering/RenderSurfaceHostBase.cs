using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Rendering;

/// <summary>
/// Represents a base class for hosting a render surface, providing functionality for managing rendering operations,
/// backbuffer access, and integration with platform-specific adapters.
/// </summary>
public abstract class RenderSurfaceHostBase : IDisposable
{
    protected readonly List<Rectangle> _overlayScreenDirty = new(16);

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
    /// ***** Note: this is SCREEN PIXELS *****
    /// </summary>
    protected internal void AddOverlayScreenDirty(Rectangle screenRect)
    {
        if (screenRect.IsEmpty)
            return;

        // If an existing rect fully contains this one, skip
        for (int i = 0; i < _overlayScreenDirty.Count; i++)
        {
            if (_overlayScreenDirty[i].Contains(screenRect))
                return;
        }

        // Merge any overlaps into one rect (and remove the overlapped ones)
        for (int i = _overlayScreenDirty.Count - 1; i >= 0; i--)
        {
            var existing = _overlayScreenDirty[i];
            if (screenRect.IntersectsWith(existing))
            {
                screenRect = Rectangle.Union(screenRect, existing);
                _overlayScreenDirty.RemoveAt(i);
            }
        }

        _overlayScreenDirty.Add(screenRect);

        // Safety valve: if a lot of tiny invalidations happen, collapse to one big rect.
        const int MaxRects = 32;
        if (_overlayScreenDirty.Count > MaxRects)
        {
            var union = _overlayScreenDirty[0];
            for (int i = 1; i < _overlayScreenDirty.Count; i++)
                union = Rectangle.Union(union, _overlayScreenDirty[i]);

            _overlayScreenDirty.Clear();
            _overlayScreenDirty.Add(union);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) => RenderSurfaceHostRegistry.Unregister(this);
}
