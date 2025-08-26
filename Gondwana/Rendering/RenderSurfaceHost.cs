using System.Diagnostics;
using System.Drawing;
using Gondwana.Rendering.Direct;
using Gondwana.Scenes;
using Gondwana.Skia;
using SkiaSharp;
using Microsoft.Extensions.Logging;

namespace Gondwana.Rendering;

public sealed class RenderSurfaceHost<TBackbuffer> : RenderSurfaceHostBase
    where TBackbuffer : BackbufferBase
{
    public event EventHandler<RenderSurfaceHostBindEventArgs>? BindToScene;

    private RenderSurfaceHost() : base() { }

    public RenderSurfaceHost(RenderSurfaceAdapterBase renderSurfaceAdapter) : this()
    {
        _renderSurfaceAdapter = renderSurfaceAdapter ?? throw new ArgumentNullException(nameof(renderSurfaceAdapter));

        // Recreate backbuffer on adapter resize
        RenderSurfaceAdapter!.Resized += (_, _) => OnRenderSurfaceAdapterResized();

        CreateBackbuffer();
    }

    /// <summary>
    /// Creates and initializes the backbuffer for the render surface. This method is called automatically
    /// during RenderSurfaceHost construction, and on <see cref="RenderSurfaceAdapter"/>.Resized, if
    /// <see cref="EngineConfiguration.RecreateBackbufferOnResize"/> is true;
    /// if it not true, this method can be called manually as needed.
    /// </summary>
    /// <remarks>This method disposes of any existing backbuffer before creating a new one with the current
    /// dimensions of the render surface. The backbuffer is initialized and prepared for rendering by calling its  <see
    /// cref="BeginFrame"/> method. If the render surface dimensions are invalid (width or height less than or equal to
    /// zero), the method exits without creating a backbuffer.</remarks>
    public void CreateBackbuffer()
    {
        Engine.Logger.LogTrace("Creating backbuffer for RenderSurfaceHost");

        var w = RenderSurfaceAdapter!.Width;
        var h = RenderSurfaceAdapter!.Height;
        if (w <= 0 || h <= 0) return;

        Backbuffer?.Dispose();
        _backbuffer = (TBackbuffer)Activator.CreateInstance(typeof(TBackbuffer), w, h)!;
        Backbuffer!.BeginFrame();

        Engine.Logger.LogTrace("Created backbuffer with size {Width}x{Height}", w, h);
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
        if (DrawSource is null || (DrawSource?.CountOfVisibleLayers ?? 0) == 0)
        {
            // Optionally mark whole surface dirty for a visible clear:
            Backbuffer.DirtyRectangle = new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height);
        }
        else
        {
            switch (DrawSource!.RefreshNeeded)
            {
                case MatrixesRefreshType.None:
                    // Nothing to redraw in the background; don’t publish a new frame.
                    // (i.e., UI will keep showing the last front buffer.)
                    break;

                case MatrixesRefreshType.Queue:
                    {
                        for (int i = DrawSource.CountOfVisibleLayers - 1; i >= 0; i--)
                        {
                            var layer = DrawSource.VisibleSceneLayerList[i];

                            // Draw tiles in this layer’s queue
                            Backbuffer.DrawTiles(layer.RefreshQueue.Tiles);
                        }

                        break;
                    }

                case MatrixesRefreshType.All:
                    {
                        // Full redraw: treat whole backbuffer as dirty
                        Backbuffer.DirtyRectangle = new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height);

                        // Clear per-layer queues and add full range, then draw
                        for (int i = DrawSource.CountOfVisibleLayers - 1; i >= 0; i--)
                        {
                            var layer = DrawSource.VisibleSceneLayerList[i];
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
    }

    internal override void RenderBackbufferToAdapter()
    {
        if (RenderSurfaceAdapter is null) return;

        Backbuffer.EndFrame();

        if (RedrawDirtyRectangleOnly)
            RenderBackbufferRect();
        else
            RenderBackbufferAll();

        Backbuffer.DirtyRectangle = Rectangle.Empty;

        Backbuffer.BeginFrame();
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
    private void OnRenderSurfaceAdapterResized()
    {
        if (Engine.Instance.Configuration.RecreateBackbufferOnResize)
            CreateBackbuffer();
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
