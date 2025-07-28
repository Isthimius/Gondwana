using SkiaSharp;
using System.Drawing;
using System.Timers;

namespace Gondwana.Rendering;

public sealed class VisibleSurface : IDisposable
{
    internal static List<VisibleSurface> _allVisibleSurfaces { get; } = new();

    public static IReadOnlyList<VisibleSurface> AllVisibleSurfaces => _allVisibleSurfaces.AsReadOnly();

    public event EventHandler<VisibleSurfaceBindEventArgs>? VisibleSurfaceBind;

    public VisibleSurface(VisibleSurfaceRenderAdapter visibleSurfaceRenderAdapter)
    {
        _allVisibleSurfaces.Add(this);

        Renderer = visibleSurfaceRenderAdapter;
    }

    public BackbufferBase? Backbuffer { get; private set; }

    public VisibleSurfaceRenderAdapter? Renderer { get; private set; } = null;

    public bool RedrawDirtyRectangleOnly { get; set; } = true;

    public void RenderBackbuffer()
    {
        if (RedrawDirtyRectangleOnly)
            RenderBackbufferRect();
        else
            RenderBackbufferAll();

        if (Backbuffer is not null)
            Backbuffer.DirtyRectangle = Rectangle.Empty;
    }

    public void Bind(BackbufferBase buffer)
    {
        var oldBuffer = Backbuffer;
        Backbuffer = buffer;

        VisibleSurfaceBind?.Invoke(this, new VisibleSurfaceBindEventArgs(oldBuffer, buffer));
    }

    #region IDisposable
    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // managed resources
                Backbuffer?.Dispose();
                _allVisibleSurfaces.Remove(this);
            }

            _disposed = true;
        }
    }

    ~VisibleSurface()
    {
        Dispose(false);
    }
    #endregion

    #region private methods
    private void RenderBackbufferAll()
    {
        if (Renderer != null)
        {
            using var snapshot = Backbuffer?.Snapshot();
            Renderer.Render(snapshot!, new SKRectI(0, 0, Backbuffer?.Width ?? 0, Backbuffer?.Height ?? 0));
        }
    }

    private void RenderBackbufferRect()
    {
        var dirty = Backbuffer?.DirtyRectangle ?? Rectangle.Empty;
        if (dirty.IsEmpty)
            return;

        if (Renderer != null)
        {
            using var snapshot = Backbuffer?.Snapshot();
            Renderer.Render(snapshot!, dirty.ToSKRectI());
        }
    }
    #endregion
}
