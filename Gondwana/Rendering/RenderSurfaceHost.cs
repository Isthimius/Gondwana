using System.Diagnostics;
using System.Drawing;
using Gondwana.Grid;
using Gondwana.Skia;
using SkiaSharp;

namespace Gondwana.Rendering;

public sealed class RenderSurfaceHost<T> : IDisposable where T : BackbufferBase
{
    internal static List<RenderSurfaceHost<T>> _allRenderSurfaceHosts { get; } = new();
    public static IReadOnlyList<RenderSurfaceHost<T>> AllRenderSurfaceHosts => _allRenderSurfaceHosts.AsReadOnly();

    public event EventHandler<RenderSurfaceHostBindEventArgs>? BindToScene;

    private RenderSurfaceHost()
    {
        _allRenderSurfaceHosts.Add(this);
    }

    public RenderSurfaceHost(RenderSurfaceAdapterBase renderSurfaceAdapter) : this()
    {
        RenderSurfaceAdapter = renderSurfaceAdapter;
        CreateBackbuffer();

        // Recreate backbuffer on adapter resize
        RenderSurfaceAdapter.Resized += (_, _) => CreateBackbuffer();
    }

    public BackbufferBase? Backbuffer { get; private set; }
    public RenderSurfaceAdapterBase? RenderSurfaceAdapter { get; private set; }
    public GridPointMatrixes? DrawSource { get; private set; }

    public void Bind(GridPointMatrixes drawSource)
    {
        if (DrawSource != null)
            DrawSource.Disposing -= OnSourceDisposing;

        var oldScene = DrawSource;
        DrawSource = drawSource;

        if (DrawSource != null)
        {
            DrawSource.Disposing += OnSourceDisposing;
            DrawSource.RefreshNeeded = MatrixesRefreshType.All;
        }

        BindToScene?.Invoke(this, new RenderSurfaceHostBindEventArgs(oldScene, DrawSource));
    }

    private void OnSourceDisposing(GridPointMatrixesDisposingEventArgs e) => DrawSource = null;

    public bool RedrawDirtyRectangleOnly { get; set; } = false;

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
        if (_disposed) return;
        if (disposing)
        {
            Backbuffer = null;
            _allRenderSurfaceHosts.Remove(this);
        }
        _disposed = true;
    }
    ~RenderSurfaceHost() => Dispose(false);
    #endregion

    #region private methods
    private void CreateBackbuffer()
    {
        Backbuffer?.Dispose();
        Backbuffer = (T)Activator.CreateInstance(typeof(T), RenderSurfaceAdapter!.Width, RenderSurfaceAdapter.Height)!;
    }

    private void RenderBackbufferAll()
    {
        if (RenderSurfaceAdapter == null || Backbuffer is null) return;

        if (Backbuffer is BitmapBackbuffer bb)
        {
            // Only publish if Engine produced a frame since last swap
            if (!bb.TryEndFrame(out var src)) return;

            var img = bb.Snapshot(); // cheap wrapper over persistent _front
            var dest = SKRect.Create(0, 0, RenderSurfaceAdapter.Width, RenderSurfaceAdapter.Height);
            RenderSurfaceAdapter.Render(img, src, dest);
            return;
        }

        // Fallback for other backbuffer types
        var snap = Backbuffer.Snapshot(); // NOTE: adapter stages prior image for disposal after paint
        var srcAll = new SKRectI(0, 0, snap.Width, snap.Height);
        var destAll = SKRect.Create(0, 0, RenderSurfaceAdapter.Width, RenderSurfaceAdapter.Height);
        RenderSurfaceAdapter.Render(snap, srcAll, destAll);
    }

    private void RenderBackbufferRect()
    {
        if (RenderSurfaceAdapter == null || Backbuffer is null) return;

        var dirty = Backbuffer.DirtyRectangle;
        if (dirty.IsEmpty)
        {
            // nothing flagged; you could fall back to full render if desired
            return;
        }

        if (Backbuffer is BitmapBackbuffer bb)
        {
            // Swap only if a new frame exists; then intersect with current dirty rect
            if (!bb.TryEndFrame(out var fullSrc)) return;

            var src = System.Drawing.Rectangle.Intersect(
                new Rectangle(fullSrc.Left, fullSrc.Top, fullSrc.Width, fullSrc.Height),
                dirty).ToSKRectI();

            if (src.IsEmpty) { Backbuffer.DirtyRectangle = Rectangle.Empty; return; }

            var img = bb.Snapshot();
            var dest = dirty.ToSKRect(); // draw to the same screen region
            RenderSurfaceAdapter.Render(img, src, dest);

            Backbuffer.DirtyRectangle = Rectangle.Empty; // reset after publish
            return;
        }

        // Fallback for other backbuffer types
        var snap2 = Backbuffer.Snapshot();
        var srcDirty = dirty.ToSKRectI();
        var destDirty = dirty.ToSKRect();
        RenderSurfaceAdapter.Render(snap2, srcDirty, destDirty);
        Backbuffer.DirtyRectangle = Rectangle.Empty;
    }
    #endregion
}
