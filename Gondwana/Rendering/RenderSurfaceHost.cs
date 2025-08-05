using Gondwana.Skia;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Rendering;

public sealed class RenderSurfaceHost : IDisposable
{
    internal static List<RenderSurfaceHost> _allRenderSurfaceHosts { get; } = new();

    public static IReadOnlyList<RenderSurfaceHost> AllRenderSurfaceHosts => _allRenderSurfaceHosts.AsReadOnly();

    public event EventHandler<RenderSurfaceHostBindEventArgs>? RenderSurfaceHostBind;

    private RenderSurfaceHost() { }

    public RenderSurfaceHost(RenderSurfaceAdapterBase visibleSurfaceRenderAdapter)
    {
        _allRenderSurfaceHosts.Add(this);
        Renderer = visibleSurfaceRenderAdapter;
    }

    public void Bind(BackbufferBase? buffer)
    {
        var oldBuffer = Backbuffer;
        Backbuffer = buffer;

        RenderSurfaceHostBind?.Invoke(this, new RenderSurfaceHostBindEventArgs(oldBuffer, buffer));
    }

    public BackbufferBase? Backbuffer { get; private set; } = null;

    public RenderSurfaceAdapterBase? Renderer { get; private set; } = null;

    public bool RedrawDirtyRectangleOnly { get; set; } = true;

    internal void RenderBackbuffer()
    {
        if (RedrawDirtyRectangleOnly)
            RenderBackbufferRect();
        else
            RenderBackbufferAll();
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
                Backbuffer = null;
                _allRenderSurfaceHosts.Remove(this);
            }

            _disposed = true;
        }
    }

    ~RenderSurfaceHost()
    {
        Dispose(false);
    }
    #endregion

    #region private methods
    private void RenderBackbufferAll()
    {
        if (Renderer != null && Backbuffer is not null)
        {
            using var snapshot = Backbuffer.Snapshot();
            Renderer.Render(snapshot, new SKRectI(0, 0, snapshot.Width, snapshot.Height));
        }
    }

    private void RenderBackbufferRect()
    {
        var dirty = Backbuffer?.DirtyRectangle ?? Rectangle.Empty;
        if (dirty.IsEmpty)
            return;

        if (Renderer != null && Backbuffer is not null)
        {
            using var snapshot = Backbuffer.Snapshot();
            Renderer.Render(snapshot, dirty.ToSKRectI());
        }
    }
    #endregion
}
