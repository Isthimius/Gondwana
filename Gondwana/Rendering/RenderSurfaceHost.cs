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

    public override BackbufferBase? Backbuffer => _backbuffer;
    public override Scene? Scene => _scene;
    public override RenderSurfaceAdapterBase? RenderSurfaceAdapter => _renderSurfaceAdapter;

    public ViewRenderer ViewRenderer { get; private set; }

    public void Bind(Scene? drawSource)
    {
        if (Scene != null)
            Scene.SceneDisposing -= OnSourceDisposing;

        var oldScene = Scene;
        _scene = drawSource;

        if (Scene != null)
        {
            ViewRenderer.BindToScene();
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
    ///     Invokes <see cref="ViewRenderer.Render"/> to update each camera, apply viewport transforms,
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
        // if there’s no Scene (or no visible layers), clear and publish the full frame
        if (Scene is null || Scene.CountOfVisibleLayers == 0)
        {
            // Erase prior overlay pixels so moving composites/particles don’t smear
            Backbuffer!.Canvas.Clear(Backbuffer.ClearColor);

            // Publish the whole surface; adapter code will blit only the dirty rect if enabled
            Backbuffer.DirtyRectangle = new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height);

            _lastTick = tick;        // keep timing consistent
            return;
        }

        var deltaSeconds = HighResTimer.GetDuration(_lastTick, tick);

        // 1) Ensure at least one view exists (default full-screen if none)
        //EnsureDefaultView();

        // 2) Build a cheap per-view state hash (camera pos, zoom, viewport rect)
        long viewsHash = 1469598103934665603L; // FNV-1a
        foreach (var v in ViewRenderer.Views)
        {
            unchecked
            {
                viewsHash ^= (long)Math.Floor(v.Camera.PositionPx.X); viewsHash *= 1099511628211L;
                viewsHash ^= (long)Math.Floor(v.Camera.PositionPx.Y); viewsHash *= 1099511628211L;
                viewsHash ^= v.Viewport.TargetRectPx.GetHashCode(); viewsHash *= 1099511628211L;
                viewsHash ^= BitConverter.SingleToInt32Bits(v.Viewport.Zoom); viewsHash *= 1099511628211L;
            }
        }

        // 4) Handle full scene refresh once
        if (Scene.RefreshNeeded == SceneRefreshType.All)
        {
            Backbuffer!.Canvas.Clear(Backbuffer.ClearColor);
            var full = new Rectangle(0, 0, RenderSurfaceAdapter!.Width, RenderSurfaceAdapter!.Height);
            for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
                Scene.VisibleSceneLayers[i].RefreshQueue.AddPixelRangeToRefreshQueue(full, cascadeToOtherRefreshQueues: false);
        }

        // 3) Fast “no work” probe: no overlay dirty, no layer queues pending, and no view change
        bool backbufferDirty = !Backbuffer!.DirtyRectangle.IsEmpty;
        bool sceneDirty = false;
        for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
        {

            if (Scene.VisibleSceneLayers[i].RefreshQueue.Tiles.Any())
            {
                sceneDirty = true;
                break;
            }
        }

        // if nothing is dirty, skip rendering this frame
        if (Scene.RefreshNeeded == SceneRefreshType.None
                                && !backbufferDirty
                                && !sceneDirty
                                && viewsHash == _lastViewsStateHash)
        {
            return;
        }

        // 5) If overlays dirtied the SCREEN, project that dirty into WORLD per view and enqueue to layers
        Rectangle screenDirty = Backbuffer.DirtyRectangle;
        if (!screenDirty.IsEmpty)
        {
            // A) Erase the old overlay pixels in SCREEN space
            var sk = new SKRect(screenDirty.Left, screenDirty.Top, screenDirty.Right, screenDirty.Bottom);
            using (new SKAutoCanvasRestore(Backbuffer!.Canvas, true))
            {
                Backbuffer.Canvas.ClipRect(sk);
                Backbuffer.Canvas.Clear(Backbuffer.ClearColor);
            }

            // B) project screen->world per view and enqueue to layer queues...
            foreach (var v in ViewRenderer.Views)
            {
                var cam = v.Camera;
                var vp = v.Viewport;
                float z = (vp.Zoom <= 0f) ? 1e-6f : vp.Zoom;

                // Screen -> World
                int wx = (int)Math.Floor(cam.PositionPx.X + (screenDirty.Left - vp.TargetRectPx.Left) * z);
                int wy = (int)Math.Floor(cam.PositionPx.Y + (screenDirty.Top - vp.TargetRectPx.Top) * z);
                int ww = (int)Math.Ceiling(screenDirty.Width * z);
                int wh = (int)Math.Ceiling(screenDirty.Height * z);
                var worldDirtyForView = new Rectangle(wx, wy, ww, wh);

                for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
                    Scene.VisibleSceneLayers[i].RefreshQueue.AddPixelRangeToRefreshQueue(worldDirtyForView, cascadeToOtherRefreshQueues: true);
            }

            // Clear the overlay dirty so it doesn’t re-trigger every frame
            Backbuffer.DirtyRectangle = Rectangle.Empty;
        }

        // 6) Render all views. Draw layers back -> front (ascending Z).
        ViewRenderer.Render(Backbuffer!.Canvas, dtSeconds: deltaSeconds, drawScene: _ =>
        {
            for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
            {
                var layer = Scene.VisibleSceneLayers[i];
                Backbuffer.DrawTiles(layer.RefreshQueue.Tiles);
            }
        });

        // 7) Compute adapter-space dirty from the tiles we actually drew (no RefreshQueue changes needed)
        //    We union per-layer, per-tile DrawLocationRefresh if available; else DrawLocation.
        Rectangle adapterDirty = Rectangle.Empty;

        // Single-view + zoom=1 + fullscreen fast path (avoid float math/divs)
        bool singleFullView =
            ViewRenderer.Views.Count == 1 &&
            Math.Abs(ViewRenderer.Views[0].Viewport.Zoom - 1f) < 1e-6 &&
            ViewRenderer.Views[0].Viewport.TargetRectPx == new Rectangle(0, 0, RenderSurfaceAdapter!.Width, RenderSurfaceAdapter!.Height);

        if (singleFullView)
        {
            var cam = ViewRenderer.Views[0].Camera;
            for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
            {
                var tiles = Scene.VisibleSceneLayers[i].RefreshQueue.Tiles;
                for (int t = 0; t < tiles.Count; t++)
                {
                    var tile = tiles[t];
                    // Prefer partial refresh; fallback to full draw rect
                    if (tile.DrawLocationRefresh is not null && tile.DrawLocationRefresh.Count > 0)
                    {
                        for (int r = 0; r < tile.DrawLocationRefresh.Count; r++)
                        {
                            // World->Screen with zoom=1, full target
                            var rr = tile.DrawLocationRefresh[r];
                            var scr = new Rectangle(rr.Left - (int)cam.PositionPx.X,
                                                    rr.Top - (int)cam.PositionPx.Y,
                                                    rr.Width, rr.Height);
                            if (!scr.IsEmpty)
                                adapterDirty = adapterDirty.IsEmpty ? scr : Rectangle.Union(adapterDirty, scr);
                        }
                    }
                    else
                    {
                        var rr = tile.DrawLocation;
                        var scr = new Rectangle(rr.Left - (int)cam.PositionPx.X,
                                                rr.Top - (int)cam.PositionPx.Y,
                                                rr.Width, rr.Height);
                        if (!scr.IsEmpty)
                            adapterDirty = adapterDirty.IsEmpty ? scr : Rectangle.Union(adapterDirty, scr);
                    }
                }
            }

            // Clamp to adapter bounds
            var fullScreen = new Rectangle(0, 0, RenderSurfaceAdapter.Width, RenderSurfaceAdapter.Height);
            adapterDirty.Intersect(fullScreen);
        }
        else
        {
            // General case: per-view, account for zoom and viewport placement
            for (int v = 0; v < ViewRenderer.Views.Count; v++)
            {
                var view = ViewRenderer.Views[v];
                var cam = view.Camera;
                var vp = view.Viewport;
                float z = (vp.Zoom <= 0f) ? 1e-6f : vp.Zoom;
                float invZ = 1f / z;

                Rectangle viewDirty = Rectangle.Empty;

                for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
                {
                    var tiles = Scene.VisibleSceneLayers[i].RefreshQueue.Tiles;
                    for (int t = 0; t < tiles.Count; t++)
                    {
                        var tile = tiles[t];

                        if (tile.DrawLocationRefresh is not null && tile.DrawLocationRefresh.Count > 0)
                        {
                            for (int r = 0; r < tile.DrawLocationRefresh.Count; r++)
                            {
                                var rr = tile.DrawLocationRefresh[r]; // world px
                                var sx = vp.TargetRectPx.Left + (int)Math.Floor((rr.Left - cam.PositionPx.X) * invZ);
                                var sy = vp.TargetRectPx.Top + (int)Math.Floor((rr.Top - cam.PositionPx.Y) * invZ);
                                var sw = (int)Math.Ceiling(rr.Width * invZ);
                                var sh = (int)Math.Ceiling(rr.Height * invZ);
                                var scr = new Rectangle(sx, sy, sw, sh);
                                if (!scr.IsEmpty)
                                    viewDirty = viewDirty.IsEmpty ? scr : Rectangle.Union(viewDirty, scr);
                            }
                        }
                        else
                        {
                            var rr = tile.DrawLocation; // world px
                            var sx = vp.TargetRectPx.Left + (int)Math.Floor((rr.Left - cam.PositionPx.X) * invZ);
                            var sy = vp.TargetRectPx.Top + (int)Math.Floor((rr.Top - cam.PositionPx.Y) * invZ);
                            var sw = (int)Math.Ceiling(rr.Width * invZ);
                            var sh = (int)Math.Ceiling(rr.Height * invZ);
                            var scr = new Rectangle(sx, sy, sw, sh);
                            if (!scr.IsEmpty)
                                viewDirty = viewDirty.IsEmpty ? scr : Rectangle.Union(viewDirty, scr);
                        }
                    }
                }

                if (!viewDirty.IsEmpty)
                {
                    viewDirty.Intersect(vp.TargetRectPx);
                    adapterDirty = adapterDirty.IsEmpty ? viewDirty : Rectangle.Union(adapterDirty, viewDirty);
                }
            }
        }

        // 8) Preserve any pre-existing dirty (e.g., set earlier this frame) — union, don’t replace
        var carry = Backbuffer.DirtyRectangle;
        Backbuffer.DirtyRectangle = adapterDirty.IsEmpty
            ? carry.IsEmpty ? new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height) : carry
            : (carry.IsEmpty ? adapterDirty : Rectangle.Union(adapterDirty, carry));

        // 9) Clear layer queues now that we’ve consumed them (avoids re-drawing same tiles next frame)
        for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
            Scene.VisibleSceneLayers[i].RefreshQueue.ClearRefreshQueue();

        // 10) Remember view state for the next fast-path check
        _lastViewsStateHash = viewsHash;

        Scene.RefreshNeeded = SceneRefreshType.None;
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

    private void OnRenderSurfaceAdapterResized()
    {
        var w = RenderSurfaceAdapter!.Width;
        var h = RenderSurfaceAdapter!.Height;

        if (Scene != null)
            Scene.RefreshNeeded = SceneRefreshType.All;   // full redraw next frame

        _backbuffer?.RequestResize(w, h);                 // UI thread → request only

        // update each viewport in MultiView to fit the new adapter dimensions.
        foreach (var view in ViewRenderer.Views)
        {
            // TODO: resize proportionally
            // Update viewport to new screen rect
            view.Viewport.TargetRectPx = new Rectangle(0, 0,
                RenderSurfaceAdapter.Width, RenderSurfaceAdapter.Height);
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