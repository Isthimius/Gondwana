using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Rendering;

/// <summary>
/// Represents a base class for hosting a render surface, providing functionality for managing rendering operations,
/// backbuffer access, and integration with platform-specific adapters.
/// </summary>
public abstract class RenderSurfaceHostBase : IDisposable
{
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
    /// Gets the platform-specific <see cref="RenderSurfaceAdapterBase"/> responsible
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

    protected readonly Dictionary<Guid, List<Rectangle>> _viewOverlayScreenDirty = new();

    /// <summary>
    /// Marks a specified rectangular region of the overlay screen as dirty for a specific view,
    /// called from DirectDrawing instances.
    /// ***** Note: this is SCREEN PIXELS *****
    /// </summary>
    protected internal void AddViewOverlayScreenDirty(View view, Rectangle screenRect)
    {
        if (view is null)
            throw new ArgumentNullException(nameof(view));

        if (screenRect.IsEmpty)
            return;

        var viewId = view.Id;

        if (!_viewOverlayScreenDirty.TryGetValue(viewId, out var list))
        {
            list = new List<Rectangle>(16);
            _viewOverlayScreenDirty[viewId] = list;
        }

        // If an existing rect fully contains this one, skip
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Contains(screenRect))
                return;
        }

        // Merge any overlaps into one rect (and remove the overlapped ones)
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var existing = list[i];
            if (screenRect.IntersectsWith(existing))
            {
                screenRect = Rectangle.Union(screenRect, existing);
                list.RemoveAt(i);
            }
        }

        list.Add(screenRect);

        // Safety valve: if a lot of tiny invalidations happen, collapse to one big rect.
        const int MaxRects = 32;
        if (list.Count > MaxRects)
        {
            var union = list[0];
            for (int i = 1; i < list.Count; i++)
                union = Rectangle.Union(union, list[i]);

            list.Clear();
            list.Add(union);
        }
    }

    /// <summary>
    /// Gets a value indicating whether any <see cref="View"/> has pending overlay invalidations.
    /// </summary>
    /// <remarks>
    /// Overlay invalidations are tracked per view in screen-pixel space via <see cref="AddViewOverlayScreenDirty(View, Rectangle)"/>.
    /// This is typically used as a fast “no work” probe to avoid skipping a frame when only view-based overlays are animating.
    /// </remarks>
    protected internal bool IsAnyViewOverlayDirty
    {
        get
        {
            foreach (var kvp in _viewOverlayScreenDirty)
            {
                if (kvp.Value.Count > 0)
                    return true;
            }

            return false;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) => RenderSurfaceHostRegistry.Unregister(this);
}
