using Gondwana.Grid;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Rendering;

public abstract class VisibleSurfaceBase : IDisposable
{
    protected VisibleSurfaceBase(int width, int height)
    {
        Width = width;
        Height = height;
        VisibleSurfaces.Add(this);
    }

    public virtual SKCanvas Canvas { get; protected internal set; } = default!;
    public virtual IBackbuffer Buffer { get; protected internal set; } = default!;

    public virtual int Height { get; protected internal set; }
    public virtual int Width { get; protected internal set; }
    public virtual bool RedrawDirtyRectangleOnly { get; protected internal set; }

    public abstract void Erase();
    public abstract void RenderBackbuffer(bool onlyDirtyRectangle);
    public abstract void Bind(GridPointMatrixes layers);

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Buffer.Dispose();
            VisibleSurfaces.Remove(this);
        }
    }

    public virtual void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
