using System.Diagnostics;
using System.Drawing;
using Gondwana.Rendering.Direct;
using Gondwana.Scenes;
using Gondwana.Skia;
using SkiaSharp;

namespace Gondwana.Rendering;

public sealed class RenderSurfaceHost<TBackbuffer> : RenderSurfaceHostBase
    where TBackbuffer : BackbufferBase
{
    public event EventHandler<RenderSurfaceHostBindEventArgs>? BindToScene;

    private RenderSurfaceHost() : base() { }

    public RenderSurfaceHost(RenderSurfaceAdapterBase renderSurfaceAdapter) : this()
    {
        _renderSurfaceAdapter = renderSurfaceAdapter;
        CreateBackbuffer();

        // Recreate backbuffer on adapter resize
        RenderSurfaceAdapter.Resized += (_, _) => CreateBackbuffer();
    }

    private TBackbuffer _backbuffer;
    private readonly Color _clear;
    private Scene? _scene;
    private RenderSurfaceAdapterBase? _renderSurfaceAdapter;

    public override BackbufferBase Backbuffer => _backbuffer;
    public override Color ClearColor => _clear;
    public override Scene? DrawSource => _scene;
    public override RenderSurfaceAdapterBase? RenderSurfaceAdapter => _renderSurfaceAdapter;

    public void Bind(Scene drawSource)
    {
        if (DrawSource != null)
            DrawSource.Disposing -= OnSourceDisposing;

        var oldScene = DrawSource;
        _scene = drawSource;

        if (DrawSource != null)
        {
            DrawSource.Disposing += OnSourceDisposing;
            DrawSource.RefreshNeeded = MatrixesRefreshType.All;
        }

        BindToScene?.Invoke(this, new RenderSurfaceHostBindEventArgs(oldScene, DrawSource));
    }

    private void OnSourceDisposing(SceneLayeresDisposingEventArgs e) => _scene = null;

    public bool RedrawDirtyRectangleOnly { get; set; } = false;

    internal override void DrawRefreshQueueToBackbuffer()
    {
        if (Backbuffer is null) return;

        // Only BitmapBackbuffer has the TryEndFrame/BeginFrame/MarkDirty helpers.
        if (Backbuffer is not BitmapBackbuffer bb)
        {
            // Legacy path: draw as you did before (optional)
            return;
        }

        var grids = DrawSource;

        // --- Begin background frame ---
        bb.BeginFrame();
        bb.ClearOpaque(SKColors.Black); // your scene clear happens here

        if (grids == null || grids.Count == 0)
        {
            // No grid: leave as just the clear (or draw any “no scene” UI here)
            // Force refresh of DirectDrawing objects, if that’s your policy:
            foreach (DirectDrawingBase drawing in DirectDrawingManager._instances)
                drawing.ForceRefresh();

            // Nothing else drawn, but we still want to publish the clear
            bb.MarkDirty();
            DirectDrawingManager.RenderAll(); // if this draws onto the backbuffer
            return;
        }

        switch (grids.RefreshNeeded)
        {
            case MatrixesRefreshType.None:
                // Nothing to redraw in the background; don’t publish a new frame.
                // (Host will keep showing the last front buffer.)
                return;

            case MatrixesRefreshType.Queue:
                {
                    // Union dirty rectangles from all visible layers into Backbuffer.DirtyRectangle
                    System.Drawing.Rectangle dirtyUnion = System.Drawing.Rectangle.Empty;

                    for (int i = grids.CountOfVisibleLayers - 1; i >= 0; i--)
                    {
                        var rq = grids.VisibleSceneLayerList[i].RefreshQueue;

                        // If you keep a list of rectangles, union them. If not, you can
                        // compute from tiles’ DrawLocation as needed.
                        foreach (var rect in rq.GetDirtyRectangles())
                            dirtyUnion = dirtyUnion.IsEmpty ? rect : System.Drawing.Rectangle.Union(dirtyUnion, rect);

                        // Draw tiles in this layer’s queue
                        bb.BeginFrame();
                        bb.DrawTiles(rq.Tiles);
                        bb.MarkDirty();
                    }

                    bb.DirtyRectangle = dirtyUnion; // engine sets it; host may use rect mode
                    bb.MarkDirty();
                    break;
                }

            case MatrixesRefreshType.All:
                {
                    // Full redraw: treat whole backbuffer as dirty
                    Backbuffer.DirtyRectangle = new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height);

                    // Clear per-layer queues and add full range, then draw
                    for (int i = grids.CountOfVisibleLayers - 1; i >= 0; i--)
                    {
                        var layer = grids.VisibleSceneLayerList[i];
                        layer.RefreshQueue.ClearRefreshQueue();
                        layer.RefreshQueue.AddPixelRangeToRefreshQueue(new Rectangle(0, 0, RenderSurfaceAdapter!.Width, RenderSurfaceAdapter!.Height), false);

                        ((BitmapBackbuffer)Backbuffer).BeginFrame();
                        Backbuffer.DrawTiles(layer.RefreshQueue.Tiles);
                        ((BitmapBackbuffer)Backbuffer).MarkDirty();
                    }

                    bb.MarkDirty();
                    break;
                }

            default:
                // Unknown state; skip
                break;
        }
    }

    internal override void RenderBackbufferToAdapter()
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

        base.Dispose(disposing);

        if (disposing)
        {
            _backbuffer = null;
        }
        _disposed = true;
    }
    ~RenderSurfaceHost() => Dispose(false);
    #endregion

    #region private methods
    private void CreateBackbuffer()
    {
        Backbuffer?.Dispose();
        _backbuffer = (TBackbuffer)Activator.CreateInstance(typeof(TBackbuffer), RenderSurfaceAdapter!.Width, RenderSurfaceAdapter.Height)!;
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
