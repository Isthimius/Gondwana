using System.Drawing;
using Gondwana.Scenes;
using Gondwana.Scenes.EventArgs;
using Gondwana.Skia;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Gondwana.Rendering;

public sealed class RenderSurfaceHost<TBackbuffer> : RenderSurfaceHostBase
    where TBackbuffer : BackbufferBase
{
    public event EventHandler<RenderSurfaceHostBindEventArgs>? BindToScene;

    private RenderSurfaceHost() : base()
    {
    }

    public RenderSurfaceHost(RenderSurfaceAdapterBase renderSurfaceAdapter) : this()
    {
        _renderSurfaceAdapter = renderSurfaceAdapter ?? throw new ArgumentNullException(nameof(renderSurfaceAdapter));

        // Recreate backbuffer on adapter resize
        RenderSurfaceAdapter!.Resized += (_, _) => OnRenderSurfaceAdapterResized();

        var w = RenderSurfaceAdapter!.Width;
        var h = RenderSurfaceAdapter!.Height;

        if (w > 0 || h > 0)
        {
            _backbuffer = (TBackbuffer)Activator.CreateInstance(typeof(TBackbuffer), w, h)!;
            Backbuffer!.BeginFrame();

            Backbuffer!.SizeChanged += (w, h) =>
            {
                if (DrawSource != null)
                    DrawSource.RefreshNeeded = SceneRefreshType.All; // full redraw at the new size
            };
        }
    }

    private TBackbuffer? _backbuffer;
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
            DrawSource.RefreshNeeded = SceneRefreshType.All;
        }

        BindToScene?.Invoke(this, new RenderSurfaceHostBindEventArgs(oldScene, DrawSource));
    }

    private void OnSourceDisposing(SceneLayeresDisposingEventArgs e) => _scene = null;

    public bool RedrawDirtyRectangleOnly { get; set; } = true;

    /// <summary>
    /// Renders the refresh queue of visible layers to the backbuffer based on the current scene's refresh state.
    /// Called as part of DoBackgroundTasks().
    /// </summary>
    /// <remarks>This method processes the visible layers of the scene and updates the backbuffer according to
    /// the  refresh requirements specified by the scene's <see cref="SceneRefreshType"/>. It handles three main
    /// refresh scenarios: <list type="bullet"> <item> <description><see cref="SceneRefreshType.None"/>: No updates are
    /// made to the backbuffer, and the last rendered frame remains visible.</description> </item> <item>
    /// <description><see cref="SceneRefreshType.Queue"/>: Only the tiles in the refresh queue of each visible layer
    /// are redrawn.</description> </item> <item> <description><see cref="SceneRefreshType.All"/>: The entire backbuffer
    /// is cleared and fully redrawn, including all visible layers.</description> </item> </list> If no visible layers
    /// are present or the <see cref="DrawSource"/> is null, the entire backbuffer is  marked as dirty to ensure a
    /// visible clear.</remarks>
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
                case SceneRefreshType.None:
                    // Nothing to redraw in the background; don’t publish a new frame.
                    // (i.e., UI will keep showing the last front buffer.)
                    break;

                case SceneRefreshType.Queue:
                    {
                        for (int i = DrawSource.CountOfVisibleLayers - 1; i >= 0; i--)
                        {
                            var layer = DrawSource.VisibleSceneLayerList[i];

                            // Draw tiles in this layer’s queue
                            Backbuffer.DrawTiles(layer.RefreshQueue.Tiles);
                        }

                        break;
                    }

                case SceneRefreshType.All:
                    {
                        // full redraw: treat whole backbuffer as dirty
                        Backbuffer.DirtyRectangle = new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height);

                        Engine.Logger.LogTrace("*** Full redraw of all layers for DirtyRectangle: {DirtyRectangle} ***", Backbuffer.DirtyRectangle.ToString());

                        // clear the backbuffer so no stale pixels survive this pass
                        Backbuffer.Canvas.Clear(Backbuffer.ClearColor);   // Canvas + ClearColor are on BackbufferBase

                        // clear per-layer queues and add full range, then draw
                        for (int i = DrawSource.CountOfVisibleLayers - 1; i >= 0; i--)
                        {
                            var layer = DrawSource.VisibleSceneLayerList[i];
                            layer.RefreshQueue.AddPixelRangeToRefreshQueue(new Rectangle(0, 0, RenderSurfaceAdapter!.Width, RenderSurfaceAdapter!.Height), false);

                            Backbuffer.DrawTiles(layer.RefreshQueue.Tiles);
                        }

                        break;
                    }

                default:
                    // unknown state; skip
                    Engine.Logger.LogWarning("Unknown Scene.RefreshNeeded state: " + DrawSource.RefreshNeeded.ToString());
                    break;
            }
        }
    }

    /// <summary>
    /// Renders the contents of the backbuffer to the associated UI adapter.
    /// Called as part of DoForegroundTasks().
    /// </summary>
    /// <remarks>This method finalizes the current frame on the backbuffer and renders its contents  to the
    /// adapter. If <see cref="RedrawDirtyRectangleOnly"/> is <see langword="true"/>, only the dirty rectangle is
    /// redrawn; otherwise, the entire backbuffer is rendered. After rendering, the dirty rectangle is reset, and the
    /// backbuffer is prepared for the next frame.</remarks>
    internal override void RenderBackbufferToAdapter()
    {
        if (RenderSurfaceAdapter is null) return;

        Backbuffer.EndFrame();

        if (RedrawDirtyRectangleOnly)
            RenderBackbufferRect();
        else
            RenderBackbufferAll();

        Backbuffer.DirtyRectangle = Rectangle.Empty; // reset

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

    #endregion IDisposable

    #region private methods

    private void OnRenderSurfaceAdapterResized()
    {
        //Engine.Logger.LogTrace("in OnRenderSurfaceAdapterResized()");

        var w = RenderSurfaceAdapter!.Width;
        var h = RenderSurfaceAdapter!.Height;

        if (DrawSource != null)
        {
            DrawSource.RefreshNeeded = SceneRefreshType.All; // full redraw next frame
            Engine.Logger.LogTrace("*** .RefreshNeeded = SceneRefreshType.All ***");
        }

        _backbuffer?.RequestResize(w, h);                 // UI thread → request only
    }

    private void RenderBackbufferAll()
    {
        var img = Backbuffer.Snapshot();
        var src = new SKRectI(0, 0, img.Width, img.Height);
        var dst = SKRect.Create(0, 0, RenderSurfaceAdapter!.Width, RenderSurfaceAdapter.Height);

        Engine.Logger.LogTrace("*** in RenderBackbufferAll()      src: {Src} dst: {Dst} ***", src.ToString(), dst.ToString());

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

    #endregion private methods
}