using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Rendering;

public abstract class RenderSurfaceHostBase : IDisposable
{
    protected RenderSurfaceHostBase() => RenderSurfaceHostRegistry.Register(this);
    ~RenderSurfaceHostBase() => Dispose(false);

    public abstract BackbufferBase Backbuffer { get; }
    public abstract Scene? DrawSource { get; }
    public abstract Color ClearColor { get; }
    public abstract RenderSurfaceAdapterBase? RenderSurfaceAdapter { get; }

    /// <summary>
    /// Runs as part of DoBackgroundTasks. Takes content of RefreshQueue
    /// - which is a queue of tiles that need to be redrawn -
    /// and draws them to the backbuffer. This, in turn, updates the
    /// Backbuffer.DirtyRectangle.
    /// </summary>
    internal abstract void DrawRefreshQueueToBackbuffer();

    /// <summary>
    /// Runs as part of DoForegroundTasks. This renders the DirtyRectangle
    /// area of the backbuffer to the adapter.
    /// </summary>
    internal abstract void RenderBackbufferToAdapter();

    public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
    protected virtual void Dispose(bool disposing) { RenderSurfaceHostRegistry.Unregister(this); }
}
