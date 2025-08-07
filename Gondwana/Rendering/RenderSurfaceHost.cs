using Gondwana.Grid;
using Gondwana.Skia;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Rendering;

public sealed class RenderSurfaceHost<T> : IDisposable where T : BackbufferBase
{
    internal static List<RenderSurfaceHost<T>> _allRenderSurfaceHosts { get; } = new();

    public static IReadOnlyList<RenderSurfaceHost<T>> AllRenderSurfaceHosts => _allRenderSurfaceHosts.AsReadOnly();

    private RenderSurfaceHost()
    {
        _allRenderSurfaceHosts.Add(this);
    }

    public RenderSurfaceHost(RenderSurfaceAdapterBase renderSurfaceAdapter) : this()
    {
        RenderSurfaceAdapter = renderSurfaceAdapter;
        CreateBackbuffer();

        // create new backbuffer on resize
        RenderSurfaceAdapter.Resized += (_, _) => CreateBackbuffer();
    }

    public BackbufferBase? Backbuffer { get; private set; } = null;

    public RenderSurfaceAdapterBase? RenderSurfaceAdapter { get; private set; } = null;

    public GridPointMatrixes? DrawSource { get; private set; } = null;

    public void Bind(GridPointMatrixes drawSource)
    {
        if (DrawSource != null)
            DrawSource.Disposing -= OnSourceDisposing;

        DrawSource = drawSource;

        if (DrawSource != null)
        {
            DrawSource.Disposing += OnSourceDisposing;
            DrawSource.RefreshNeeded = MatrixesRefreshType.All;
        }
    }

    private void OnSourceDisposing(GridPointMatrixesDisposingEventArgs e) => DrawSource = null;

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
    private void CreateBackbuffer()
    {
        // Dispose the old backbuffer if it exists
        Backbuffer?.Dispose();

        // Use Activator.CreateInstance to create T with parameters
        Backbuffer = (T)Activator.CreateInstance(typeof(T), RenderSurfaceAdapter!.Width, RenderSurfaceAdapter.Height)!;
    }

    private void RenderBackbufferAll()
    {
        if (RenderSurfaceAdapter != null && Backbuffer is not null)
        {
            using var snapshot = Backbuffer.Snapshot();
            RenderSurfaceAdapter.Render(snapshot, new SKRectI(0, 0, snapshot.Width, snapshot.Height));
        }
    }

    private void RenderBackbufferRect()
    {
        var dirty = Backbuffer?.DirtyRectangle ?? Rectangle.Empty;
        if (dirty.IsEmpty)
            return;

        if (RenderSurfaceAdapter != null && Backbuffer is not null)
        {
            using var snapshot = Backbuffer.Snapshot();
            RenderSurfaceAdapter.Render(snapshot, dirty.ToSKRectI());
        }
    }
    #endregion
}
