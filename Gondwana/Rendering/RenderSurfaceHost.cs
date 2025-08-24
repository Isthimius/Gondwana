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
        RenderSurfaceAdapter!.Resized += (_, _) => CreateBackbuffer();
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

    public bool RedrawDirtyRectangleOnly { get; set; } = true;

    internal override void DrawRefreshQueueToBackbuffer()
    {
        if (Backbuffer is not BitmapBackbuffer bb) return;
        var scene = DrawSource;

        bb.BeginFrame();

        if (scene is null || (scene?.CountOfVisibleLayers ?? 0) == 0)
        {
            bb.ClearOpaque(ClearColor.ToSKColor());
            // Optionally mark whole surface dirty for a visible clear:
            Backbuffer.DirtyRectangle = new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height);
        }
        else
        {
            switch (scene!.RefreshNeeded)
            {
                case MatrixesRefreshType.None:
                    // Nothing to redraw in the background; don’t publish a new frame.
                    // (i.e., UI will keep showing the last front buffer.)
                    return;

                case MatrixesRefreshType.Queue:
                    {
                        for (int i = scene.CountOfVisibleLayers - 1; i >= 0; i--)
                        {
                            var layer = scene.VisibleSceneLayerList[i];

                            // Draw tiles in this layer’s queue
                            bb.DrawTiles(layer.RefreshQueue.Tiles);
                        }

                        break;
                    }

                case MatrixesRefreshType.All:
                    {
                        // Full redraw: treat whole backbuffer as dirty
                        Backbuffer.DirtyRectangle = new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height);

                        // Clear per-layer queues and add full range, then draw
                        for (int i = scene.CountOfVisibleLayers - 1; i >= 0; i--)
                        {
                            var layer = scene.VisibleSceneLayerList[i];
                            layer.RefreshQueue.AddPixelRangeToRefreshQueue(new Rectangle(0, 0, RenderSurfaceAdapter!.Width, RenderSurfaceAdapter!.Height), false);

                            Backbuffer.DrawTiles(layer.RefreshQueue.Tiles);
                        }

                        break;
                    }

                default:
                    // Unknown state; skip
                    break;
            }
        }

        bb.EndFrame();
    }

    internal override void RenderBackbufferToAdapter()
    {
        if (RenderSurfaceAdapter is null) return;

        if (RedrawDirtyRectangleOnly)
            RenderBackbufferRect();
        else
            RenderBackbufferAll();

        Backbuffer.DirtyRectangle = Rectangle.Empty;
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
        var img = Backbuffer.Snapshot();
        var src = new SKRectI(0, 0, img.Width, img.Height);
        var dst = SKRect.Create(0, 0, RenderSurfaceAdapter!.Width, RenderSurfaceAdapter.Height);

        // Post to UI thread
        Engine.Instance.UiDispatcher!.Post(() => RenderSurfaceAdapter.Render(img, src, dst));
    }

    private void RenderBackbufferRect()
    {
        var dirty = Backbuffer.DirtyRectangle;
        if (dirty.IsEmpty) return;

        var img = Backbuffer.Snapshot();

        // Post to UI thread
        Engine.Instance.UiDispatcher!.Post(() => RenderSurfaceAdapter!.Render(img, dirty.ToSKRectI(), dirty.ToSKRect()));
    }
    #endregion
}
