using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Rendering;

public abstract class RenderSurfaceHostBase : IDisposable
{
    protected RenderSurfaceHostBase() => RenderSurfaceHostRegistry.Register(this);
    ~RenderSurfaceHostBase() => Dispose(false);

    public abstract BackbufferBase Backbuffer { get; }
    public abstract RefreshQueue RefreshQueue { get; }
    public abstract Scene? DrawSource { get; }
    public abstract Color ClearColor { get; }
    public abstract RenderSurfaceAdapterBase? RenderSurfaceAdapter { get; }

    internal abstract void RenderBackbuffer();

    public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
    protected virtual void Dispose(bool disposing) { RenderSurfaceHostRegistry.Unregister(this); }
}
