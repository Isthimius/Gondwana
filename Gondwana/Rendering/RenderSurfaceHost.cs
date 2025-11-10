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
    private readonly RenderSurfaceAdapterBase? _renderSurfaceAdapter;

    public override BackbufferBase? Backbuffer => _backbuffer;
    public override Scene? Scene => _scene;
    public override RenderSurfaceAdapterBase? RenderSurfaceAdapter => _renderSurfaceAdapter;

    public void Bind(Scene? drawSource)
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
    /// Renders all visible scene layers for every configured view onto the backbuffer.
    /// <para>
    /// This is the unified render path — it handles both single- and multi-view rendering.
    /// It performs the following steps each frame:
    /// <list type="number">
    ///   <item><description>
    ///     Ensures at least one <see cref="View"/> exists (creating a default full-screen view if necessary).
    ///   </description></item>
    ///   <item><description>
    ///     If a full scene refresh is requested, clears the backbuffer and fills each layer’s refresh queue
    ///     with a rectangle covering the entire render surface.
    ///   </description></item>
    ///   <item><description>
    ///     Computes the union of all layer refresh regions (in world pixels) and transforms that union into
    ///     screen-space rectangles per view, producing a final adapter-space dirty rectangle.
    ///   </description></item>
    ///   <item><description>
    ///     Invokes <see cref="MultiViewRenderer.Render"/> to update each camera, apply viewport transforms,
    ///     and draw every visible layer’s tiles in ascending Z order (back → front).
    ///   </description></item>
    ///   <item><description>
    ///     Updates <see cref="BackbufferBase.DirtyRectangle"/> with the final screen-space union so that only
    ///     the necessary portion of the backbuffer is blitted to the adapter.
    ///   </description></item>
    /// </list>
    /// This method is called once per frame by <see cref="RenderSurfaceHostBase"/> and should never be invoked directly.
    /// </para>
    /// </summary>
    internal override void DrawRefreshQueueToBackbuffer(long tick)
    {
        if (Scene is null || Scene.CountOfVisibleLayers == 0)
        {
            Backbuffer!.DirtyRectangle = new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height);
            return;
        }

        // 1) Ensure at least one view exists (full-screen default if none)
        EnsureDefaultView();

        var deltaSeconds = HighResTimer.GetDuration(_lastTick, tick);

        // 2) Handle full scene refresh (prefill queues once)
        if (Scene.RefreshNeeded == SceneRefreshType.All)
        {
            Backbuffer!.Canvas.Clear(Backbuffer.ClearColor);
            var full = new Rectangle(0, 0, RenderSurfaceAdapter!.Width, RenderSurfaceAdapter!.Height);
            for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
                Scene.VisibleSceneLayers[i].RefreshQueue.AddPixelRangeToRefreshQueue(full, cascadeToOtherRefreshQueues: false);
        }

        // 3) If overlays/DirectDrawings dirtied the SCREEN, project that into WORLD per view
        //    and enqueue to each visible layer so tiles under/around overlays repaint.
        Rectangle screenDirty = Backbuffer.DirtyRectangle;
        if (!screenDirty.IsEmpty)
        {
            foreach (var v in _multiView.Views)
            {
                var cam = v.Camera;
                var vp = v.Viewport;
                float z = (vp.Zoom <= 0f) ? 1e-6f : vp.Zoom;

                // Screen -> World:
                int wx = (int)Math.Floor(cam.PositionPx.X + (screenDirty.Left - vp.TargetRectPx.Left) * z);
                int wy = (int)Math.Floor(cam.PositionPx.Y + (screenDirty.Top - vp.TargetRectPx.Top) * z);
                int ww = (int)Math.Ceiling(screenDirty.Width * z);
                int wh = (int)Math.Ceiling(screenDirty.Height * z);

                var worldDirtyForView = new Rectangle(wx, wy, ww, wh);

                for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
                    Scene.VisibleSceneLayers[i].RefreshQueue.AddPixelRangeToRefreshQueue(worldDirtyForView, cascadeToOtherRefreshQueues: true);
            }
        }

        // 4) Compute WORLD-space dirty union across visible layers (includes step 3 additions)
        Rectangle worldDirty = Rectangle.Empty;
        for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
        {
            var layer = Scene.VisibleSceneLayers[i];
            var d = layer.RefreshQueue.GetWorldDirtyBoundsPx(); // requires the small helper added to RefreshQueue
            if (!d.IsEmpty)
                worldDirty = worldDirty.IsEmpty ? d : Rectangle.Union(worldDirty, d);
        }

        // 5) Render all views. Draw layers back->front (ascending Z).
        _multiView.Render(Backbuffer!.Canvas, dtSeconds: deltaSeconds, drawScene: _ =>
        {
            for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
            {
                var layer = Scene.VisibleSceneLayers[i];
                Backbuffer.DrawTiles(layer.RefreshQueue.Tiles);
            }
        });

        // 6) Transform WORLD dirty -> per-view SCREEN dirty and union into adapter-space rect
        Rectangle adapterDirty = Rectangle.Empty;
        if (!worldDirty.IsEmpty)
        {
            foreach (var v in _multiView.Views)
            {
                var cam = v.Camera;
                var vp = v.Viewport;
                float z = (vp.Zoom <= 0f) ? 1e-6f : vp.Zoom;

                // World -> Screen (inside viewport)
                int sx = vp.TargetRectPx.Left + (int)Math.Floor((worldDirty.Left - cam.PositionPx.X) / z);
                int sy = vp.TargetRectPx.Top + (int)Math.Floor((worldDirty.Top - cam.PositionPx.Y) / z);
                int sw = (int)Math.Ceiling(worldDirty.Width / z);
                int sh = (int)Math.Ceiling(worldDirty.Height / z);

                var viewDirty = new Rectangle(sx, sy, sw, sh);
                viewDirty.Intersect(vp.TargetRectPx); // clamp to the viewport window

                if (!viewDirty.IsEmpty)
                    adapterDirty = adapterDirty.IsEmpty ? viewDirty : Rectangle.Union(adapterDirty, viewDirty);
            }
        }

        // 7) Preserve any existing screen-space dirty (e.g., DirectDrawings) — union, don't replace
        var ddDirty = Backbuffer.DirtyRectangle;
        var finalDirty = adapterDirty.IsEmpty
            ? ddDirty
            : (ddDirty.IsEmpty ? adapterDirty : Rectangle.Union(adapterDirty, ddDirty));

        Backbuffer.DirtyRectangle = finalDirty.IsEmpty
            ? new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height) // fallback (first frame, etc.)
            : finalDirty;

        _lastTick = tick;
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

        Backbuffer!.EndFrame();

        if (MultiViewEnabled)
        {
            // multi-view: publish full frame
            RenderBackbufferAll();
        }
        else
        {
            if (RedrawDirtyRectangleOnly)
                RenderBackbufferRect();
            else
                RenderBackbufferAll();
        }

        Backbuffer.DirtyRectangle = Rectangle.Empty;
        Backbuffer.BeginFrame();
    }

    #region Multiview support

    // near the other fields
    private MultiViewRenderer _multiView = new();
    public bool MultiViewEnabled => _multiView.Views.Count > 0;

    // helper for setup from the outside (build views elsewhere and add here)
    public void AddView(View view) => _multiView.AddView(view);

    public void ClearViews() => _multiView = new MultiViewRenderer();

    #endregion Multiview support

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
            Scene.RefreshNeeded = SceneRefreshType.All;   // full redraw next frame

        _backbuffer?.RequestResize(w, h);                 // UI thread → request only
    }

    /// <summary>
    /// Ensure at least one view exists. If none are configured, create a full-screen
    /// default View (Camera + Viewport) bound to the current Scene and adapter size.
    /// </summary>
    private void EnsureDefaultView()
    {
        if (_multiView.Views.Count > 0)
            return;

        if (Scene is null || RenderSurfaceAdapter is null)
            return;

        var cam = new Camera(Scene)
        {
            // Safe clamp box: use adapter size as the initial world bounds.
            // You can replace with your map/world size later.
            WorldBoundsPx = new RectangleF(0, 0, RenderSurfaceAdapter.Width, RenderSurfaceAdapter.Height),
            FollowLerpPerSecond = 0f // snap by default
        };

        cam.SnapTo(new PointF(0, 0));

        var vp = new Viewport
        {
            TargetRectPx = new Rectangle(0, 0, RenderSurfaceAdapter.Width, RenderSurfaceAdapter.Height),
            Zoom = 1f
        };

        _multiView.AddView(new View(cam, vp));
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