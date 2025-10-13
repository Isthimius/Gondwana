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
                if (Scene != null)
                    Scene.RefreshNeeded = SceneRefreshType.All; // full redraw at the new size
            };
        }
    }

    private TBackbuffer? _backbuffer;
    private Scene? _scene;
    private RenderSurfaceAdapterBase? _renderSurfaceAdapter;

    public override BackbufferBase? Backbuffer => _backbuffer;
    public override Scene? Scene => _scene;
    public override RenderSurfaceAdapterBase? RenderSurfaceAdapter => _renderSurfaceAdapter;

    public void Bind(Scene drawSource)
    {
        if (Scene != null)
            Scene.SceneDisposing -= OnSourceDisposing;

        var oldScene = Scene;
        _scene = drawSource;

        if (Scene != null)
        {
            Scene.SceneDisposing += OnSourceDisposing;
            Scene.RefreshNeeded = SceneRefreshType.All;
        }

        BindToScene?.Invoke(this, new RenderSurfaceHostBindEventArgs(oldScene, Scene));
    }

    private void OnSourceDisposing(Scene scene) => _scene = null;

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
    /// are present or the <see cref="Scene"/> is null, the entire backbuffer is  marked as dirty to ensure a
    /// visible clear.</remarks>
    internal override void DrawRefreshQueueToBackbuffer()
    {
        if (Scene is null || (Scene?.CountOfVisibleLayers ?? 0) == 0)
        {
            // Optionally mark whole surface dirty for a visible clear:
            Backbuffer.DirtyRectangle = new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height);
        }
        else
        {
            switch (Scene!.RefreshNeeded)
            {
                case SceneRefreshType.None:
                    // Nothing to redraw in the background; don’t publish a new frame.
                    // (i.e., UI will keep showing the last front buffer.)
                    break;

                case SceneRefreshType.Queue:
                    {
                        for (int i = Scene.CountOfVisibleLayers - 1; i >= 0; i--)
                        {
                            var layer = Scene.VisibleSceneLayer[i];

                            // Draw tiles in this layer’s queue
                            Backbuffer.DrawTiles(layer.RefreshQueue.Tiles);
                        }

                        break;
                    }

                case SceneRefreshType.All:
                    {
                        // full redraw: treat whole backbuffer as dirty
                        Backbuffer.DirtyRectangle = new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height);

                        // clear the backbuffer so no stale pixels survive this pass
                        Backbuffer.Canvas.Clear(Backbuffer.ClearColor);   // Canvas + ClearColor are on BackbufferBase

                        // clear per-layer queues and add full range, then draw
                        for (int i = Scene.CountOfVisibleLayers - 1; i >= 0; i--)
                        {
                            var layer = Scene.VisibleSceneLayer[i];
                            layer.RefreshQueue.AddPixelRangeToRefreshQueue(new Rectangle(0, 0, RenderSurfaceAdapter!.Width, RenderSurfaceAdapter!.Height), false);

                            Backbuffer.DrawTiles(layer.RefreshQueue.Tiles);
                        }
                        
                        break;
                    }

                default:
                    // unknown state; skip
                    Engine.Logger.LogWarning("Unknown Scene.RefreshNeeded state: {RefreshNeededState}", Scene.RefreshNeeded.ToString());
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
        var w = RenderSurfaceAdapter!.Width;
        var h = RenderSurfaceAdapter!.Height;

        if (Scene != null)
            Scene.RefreshNeeded = SceneRefreshType.All; // full redraw next frame

        _backbuffer?.RequestResize(w, h);                 // UI thread → request only
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

    #endregion private methods
}