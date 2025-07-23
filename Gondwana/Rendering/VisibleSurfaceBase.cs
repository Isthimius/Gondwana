using SkiaSharp;

namespace Gondwana.Rendering;

public abstract class VisibleSurfaceBase : IDisposable
{
    protected internal static List<VisibleSurfaceBase> _allVisibleSurfaces { get; } = new();

    public static IReadOnlyList<VisibleSurfaceBase> AllVisibleSurfaces => _allVisibleSurfaces.AsReadOnly();

    private bool _disposed;

    protected VisibleSurfaceBase(int width, int height)
    {
        Width = width;
        Height = height;
        _allVisibleSurfaces.Add(this);
    }

    public virtual BackbufferBase? Backbuffer { get; protected internal set; }

    public virtual int Height { get; protected internal set; }
    public virtual int Width { get; protected internal set; }
    public virtual bool RedrawDirtyRectangleOnly { get; protected internal set; }

    public abstract void Bind(BackbufferBase buffer);
    public abstract void Erase();
    public abstract void RenderBackbuffer(SKImage snapshot, bool onlyDirtyRectangle);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                Backbuffer?.Dispose();
            }

            _allVisibleSurfaces.Remove(this);
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~VisibleSurfaceBase()
    {
        Dispose(false);
    }
}
