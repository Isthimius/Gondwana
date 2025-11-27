using System.Drawing;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Gondwana.Scenes;
using Gondwana.Skia;
using Gondwana.Timers;

namespace Gondwana.Rendering;

public sealed class RenderSurfaceHost<TBackbuffer> : RenderSurfaceHostBase
    where TBackbuffer : BackbufferBase
{
    private long _lastTick = HighResTimer.GetCurrentTick();
    private long _lastViewsStateHash = 0;

    private TBackbuffer? _backbuffer;
    private Scene? _scene;

    private readonly RenderSurfaceAdapterBase? _renderSurfaceAdapter;

    public event EventHandler<RenderSurfaceHostBindEventArgs>? BindToScene;

    private RenderSurfaceHost() : base() { }

    public RenderSurfaceHost(RenderSurfaceAdapterBase renderSurfaceAdapter) : this()
    {
        _renderSurfaceAdapter = renderSurfaceAdapter ?? throw new ArgumentNullException(nameof(renderSurfaceAdapter));

        ViewRenderer = new ViewRenderer(this);

        // Recreate backbuffer on adapter resize
        RenderSurfaceAdapter!.Resized += (args) => OnRenderSurfaceAdapterResized(args);

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

    public override BackbufferBase? Backbuffer => _backbuffer;
    public override Scene? Scene => _scene;
    public override RenderSurfaceAdapterBase? RenderSurfaceAdapter => _renderSurfaceAdapter;

    public ViewRenderer ViewRenderer { get; private set; }

    public void Bind(Scene? drawSource, bool limitCameraToWorldBoundPx = true)
    {
        if (Scene != null)
            Scene.SceneDisposing -= OnSourceDisposing;

        var oldScene = Scene;
        _scene = drawSource;

        if (Scene != null)
        {
            ViewRenderer.BindToScene(Scene, limitCameraToWorldBoundPx);
            Scene.SceneDisposing += OnSourceDisposing;
            Scene.RefreshNeeded = SceneRefreshType.All;
        }

        BindToScene?.Invoke(this, new RenderSurfaceHostBindEventArgs(oldScene, Scene));
    }

    private void OnSourceDisposing(Scene scene) => _scene = null;

    public bool RedrawDirtyRectangleOnly { get; set; } = true;

    /// <summary>
    /// Renders all visible scene layers for every configured view onto the backbuffer.
    /// Called as part of DoBackgroundTasks().
    /// </summary>
    internal override void DrawRefreshQueueToBackbuffer(long tick)
    {
        // 1) If there’s no Scene (or no visible layers), clear and publish the full frame.
        if (!HasRenderableScene())
        {
            ClearBackbufferToFullFrame();
            _lastTick = tick;
            return;
        }

        var deltaSeconds = HighResTimer.GetDuration(_lastTick, tick);

        // Are we in a "force full redraw" situation (camera moved, zoom changed, etc.)?
        bool forceFullRedraw = Scene!.RefreshNeeded == SceneRefreshType.All;

        // 2) Handle full scene refresh once: clear and mark all layers as dirty.
        //    This already clears the whole backbuffer and enqueues a full rect per layer.
        if (forceFullRedraw)
            EnqueueFullSceneRefresh();

        // 3) Fast “no work” probe: no overlay dirty, no layer queues pending.
        bool backbufferDirty = !Backbuffer!.DirtyRectangle.IsEmpty;
        bool sceneDirty = HasSceneDirty();

        // If we are NOT forcing a full redraw and nothing is dirty, bail out.
        if (!forceFullRedraw
            && Scene.RefreshNeeded == SceneRefreshType.Tiles
            && !backbufferDirty
            && !sceneDirty)
        {
            _lastTick = tick; // keep deltaSeconds sane next frame
            return;
        }

        // 4) If overlays dirtied the SCREEN, project that dirty into WORLD per view and enqueue to layers
        ProcessOverlayScreenDirty();

        // 5) Render all views. Draw layers back -> front (ascending Z).
        ViewRenderer.Render(Backbuffer.Canvas, deltaSeconds, Scene!, (view, layer) =>
        {
            Backbuffer.DrawTiles(layer.RefreshQueue.Tiles);
        });

        // 6) Compute adapter-space dirty from the tiles we actually drew
        var adapterDirty = ComputeAdapterDirtyRectangle();

        // 7) Preserve any pre-existing dirty (e.g., set earlier this frame) — union, don’t replace.
        //    If this was a full redraw, mark the entire backbuffer as dirty so the adapter blits all of it.
        var carry = Backbuffer.DirtyRectangle;

        if (forceFullRedraw)
        {
            Backbuffer.DirtyRectangle = new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height);
        }
        else
        {
            Backbuffer.DirtyRectangle = adapterDirty.IsEmpty
                ? (carry.IsEmpty
                    ? new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height)
                    : carry)
                : (carry.IsEmpty
                    ? adapterDirty
                    : Rectangle.Union(adapterDirty, carry));
        }

        // 8) Clear layer queues now that we’ve consumed them (avoids re-drawing same tiles next frame)
        for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
            Scene.VisibleSceneLayers[i].RefreshQueue.ClearRefreshQueue();

        Scene.RefreshNeeded = SceneRefreshType.Tiles;
        _lastTick = tick;
    }

    #region DrawRefreshQueueToBackbuffer helpers

    private bool HasRenderableScene() => Scene is not null && Scene.CountOfVisibleLayers > 0;

    private void ClearBackbufferToFullFrame()
    {
        Backbuffer!.Canvas.Clear(Backbuffer.ClearColor);
        Backbuffer.DirtyRectangle = new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height);
    }

    /// <summary>
    /// Clears the backbuffer and enqueues a full-surface dirty rect into each visible layer.
    /// Used when Scene.RefreshNeeded == All.
    /// </summary>
    private void EnqueueFullSceneRefresh()
    {
        Backbuffer!.Canvas.Clear(Backbuffer.ClearColor);
        var scene = Scene!; // local for clarity

        foreach (var view in ViewRenderer.Views)
        {
            EnqueueFullSceneRefreshForView(view, scene);
        }
    }

    private void EnqueueFullSceneRefreshForView(View view, Scene scene)
    {
        var viewport = view.Viewport;
        var camera = view.Camera;
        var screenRect = viewport.TargetRectPx;

        float zoom = GetZoom(viewport);
        float offsetX = viewport.TargetRectPx.Left + viewport.ScreenOffsetPx.X;
        float offsetY = viewport.TargetRectPx.Top + viewport.ScreenOffsetPx.Y;

        for (int i = 0; i < scene.CountOfVisibleLayers; i++)
        {
            var layer = scene.VisibleSceneLayers[i];
            float parallax = layer.Parallax;

            //
            // 1) Compute layer-specific visible world rect
            //
            float worldLeft = (screenRect.Left - offsetX) * zoom + camera.PositionPx.X * parallax;
            float worldTop = (screenRect.Top - offsetY) * zoom + camera.PositionPx.Y * parallax;
            float worldRight = (screenRect.Right - offsetX) * zoom + camera.PositionPx.X * parallax;
            float worldBottom = (screenRect.Bottom - offsetY) * zoom + camera.PositionPx.Y * parallax;

            var layerWorldRect = RectangleF.FromLTRB(worldLeft, worldTop, worldRight, worldBottom);

            //
            // 2) Expand world rect by **one full tile** in all directions.
            //    This compensates for fractional-layer motion (parallax),
            //    camera motion, and tile boundaries.
            //
            int expandX = layer.SceneLayerTileWidth;
            int expandY = layer.SceneLayerTileHeight;

            layerWorldRect.Inflate(expandX, expandY);

            //
            // 3) Round to ints and enqueue
            //
            var worldRectInt = Rectangle.Round(layerWorldRect);

            layer.RefreshQueue.AddPixelRangeToRefreshQueue(
                worldRectInt,
                cascadeToOtherRefreshQueues: false);
        }
    }

    private float GetZoom(Viewport vp) => (vp.Zoom <= 0f ? 1f : vp.Zoom);

    private bool HasSceneDirty()
    {
        var scene = Scene!;
        for (int i = 0; i < scene.CountOfVisibleLayers; i++)
        {
            if (scene.VisibleSceneLayers[i].RefreshQueue.Tiles.Any())
                return true;
        }
        return false;
    }

    /// <summary>
    /// If Backbuffer.DirtyRectangle is non-empty (overlays / particles / composites),
    /// clears that region in SCREEN space and projects it back into WORLD space per view,
    /// enqueuing dirty rects into each layer’s RefreshQueue.
    /// </summary>
    private void ProcessOverlayScreenDirty()
    {
        Rectangle screenDirty = Backbuffer!.DirtyRectangle;
        if (screenDirty.IsEmpty)
            return;

        // A) Erase the old overlay pixels in SCREEN space
        EraseOverlayRegion(screenDirty);

        // B) Project screen->world per view and enqueue to layer queues...
        var scene = Scene!;
        foreach (var v in ViewRenderer.Views)
        {
            EnqueueOverlayToLayersForView(v, scene, screenDirty);
        }
    }

    private void EraseOverlayRegion(Rectangle screenDirty)
    {
        var sk = new SKRect(screenDirty.Left, screenDirty.Top, screenDirty.Right, screenDirty.Bottom);
        using (new SKAutoCanvasRestore(Backbuffer!.Canvas, true))
        {
            Backbuffer.Canvas.ClipRect(sk);
            Backbuffer.Canvas.Clear(Backbuffer.ClearColor);
        }
    }

    private void EnqueueOverlayToLayersForView(View v, Scene scene, Rectangle screenDirty)
    {
        var cam = v.Camera;
        var vp = v.Viewport;
        float z = (vp.Zoom <= 0f) ? 1e-6f : vp.Zoom;

        // Screen -> World (integer conservative bounds)
        int wx = (int)Math.Floor(cam.PositionPx.X + (screenDirty.Left - vp.TargetRectPx.Left - vp.ScreenOffsetPx.X) * z);
        int wy = (int)Math.Floor(cam.PositionPx.Y + (screenDirty.Top - vp.TargetRectPx.Top - vp.ScreenOffsetPx.Y) * z);
        int ww = (int)Math.Ceiling(screenDirty.Width * z);
        int wh = (int)Math.Ceiling(screenDirty.Height * z);
        var worldDirtyForView = new Rectangle(wx, wy, ww, wh);

        for (int i = 0; i < scene.CountOfVisibleLayers; i++)
        {
            scene.VisibleSceneLayers[i]
                 .RefreshQueue
                 .AddPixelRangeToRefreshQueue(worldDirtyForView, cascadeToOtherRefreshQueues: true);
        }
    }

    /// <summary>
    /// Computes the union of all tile dirty areas projected into adapter/screen space,
    /// using the single-view fast path when possible.
    /// </summary>
    private Rectangle ComputeAdapterDirtyRectangle()
    {
        // Union of all view dirty regions in ADAPTER/SCREEN space
        Rectangle adapterDirty = Rectangle.Empty;
        var scene = Scene!;

        for (int v = 0; v < ViewRenderer.Views.Count; v++)
        {
            var view = ViewRenderer.Views[v];

            // Delegate per-view work to helper (unit-testable)
            var viewDirty = RenderSurfaceHostHelpers.ComputeViewDirtyRectangle(view, scene);

            if (!viewDirty.IsEmpty)
            {
                adapterDirty = adapterDirty.IsEmpty
                    ? viewDirty
                    : Rectangle.Union(adapterDirty, viewDirty);
            }
        }

        return adapterDirty;
    }

    #endregion

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

        Backbuffer!.EndFrame();

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

    #endregion IDisposable

    #region private methods

    private void OnRenderSurfaceAdapterResized(RenderSurfaceAdapterResizedEventArgs args)
    {
        if (Scene != null)
            Scene.RefreshNeeded = SceneRefreshType.All;                 // full redraw next frame

        _backbuffer?.RequestResize(args.NewWidth, args.NewHeight);      // UI thread → request only

        float scaleX = (float)args.NewWidth / args.OldWidth;
        float scaleY = (float)args.NewHeight / args.OldHeight;

        // resize each View proportionally
        foreach (var view in ViewRenderer.Views)
        {
            var old = view.Viewport.TargetRectPx;

            int newLeft = (int)Math.Round(old.Left * scaleX);
            int newTop = (int)Math.Round(old.Top * scaleY);
            int newWidth = (int)Math.Round(old.Width * scaleX);
            int newHeight = (int)Math.Round(old.Height * scaleY);

            view.Viewport.TargetRectPx = new Rectangle(
                newLeft, newTop, newWidth, newHeight);
        }
    }

    private void RenderBackbufferAll()
    {
        var img = Backbuffer!.Snapshot();
        var src = new SKRectI(0, 0, img.Width, img.Height);
        var dst = SKRect.Create(0, 0, RenderSurfaceAdapter!.Width, RenderSurfaceAdapter.Height);

        // Post to UI thread
        Engine.Instance.UiDispatcher!.Post(() => RenderSurfaceAdapter.Render(img, src, dst));
    }

    private void RenderBackbufferRect()
    {
        if (Backbuffer == null)
            return;

        var dirty = Backbuffer!.DirtyRectangle;
        if (dirty.IsEmpty)
            return;

        var img = Backbuffer.Snapshot();

        // Post to UI thread
        Engine.Instance.UiDispatcher!.Post(() => RenderSurfaceAdapter!.Render(img, dirty.ToSKRectI(), dirty.ToSKRect()));
    }

    #endregion private methods
}