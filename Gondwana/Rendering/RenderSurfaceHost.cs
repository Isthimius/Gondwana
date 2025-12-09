using System.Drawing;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Gondwana.Scenes;
using Gondwana.Skia;
using Gondwana.Timers;
using Gondwana.SkiaSharp;

namespace Gondwana.Rendering;

public sealed class RenderSurfaceHost<TBackbuffer> : RenderSurfaceHostBase
    where TBackbuffer : BackbufferBase
{
    private long _lastTick = HighResTimer.GetCurrentTick();

    private TBackbuffer? _backbuffer;
    private Scene? _scene;

    private readonly SKPaint _overlayClearPaint = new()
    {
        IsAntialias = false,
        BlendMode = SKBlendMode.Src
    };

    private readonly RenderSurfaceAdapterBase? _renderSurfaceAdapter;
    private readonly ViewRenderer? _viewRenderer;

    public event EventHandler<RenderSurfaceHostBindEventArgs>? BindToScene;

    private RenderSurfaceHost() : base() { }

    public RenderSurfaceHost(RenderSurfaceAdapterBase renderSurfaceAdapter) : this()
    {
        _renderSurfaceAdapter = renderSurfaceAdapter ?? throw new ArgumentNullException(nameof(renderSurfaceAdapter));
        _viewRenderer = new ViewRenderer(this);

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
                    Scene.FullRefreshNeeded = true;
            };
        }
    }

    public override BackbufferBase? Backbuffer => _backbuffer;
    
    public override Scene? Scene => _scene;
    
    public override RenderSurfaceAdapterBase? RenderSurfaceAdapter => _renderSurfaceAdapter;

    public override ViewRenderer ViewRenderer => _viewRenderer;

    public bool RedrawDirtyRectangleOnly { get; set; } = true;

    public void Bind(Scene? drawSource, bool limitCameraToWorldBoundPx = true)
    {
        // Unregister from the old scene (if any)
        if (_scene != null)
        {
            _scene.SceneDisposing -= OnSourceDisposing;
            _scene.UnregisterRenderSurfaceHost(this);
        }

        var oldScene = _scene;
        _scene = drawSource;

        if (_scene != null)
        {
            // Register with the new scene
            _scene.RegisterRenderSurfaceHost(this);

            ViewRenderer.BindToScene(_scene, limitCameraToWorldBoundPx);
            _scene.SceneDisposing += OnSourceDisposing;
            _scene.FullRefreshNeeded = true;
        }

        BindToScene?.Invoke(this, new RenderSurfaceHostBindEventArgs(oldScene, _scene));
    }

    /// <summary>
    /// Renders all visible scene layers for every configured view onto the backbuffer.
    /// Called as part of DoForegroundTasks().
    /// </summary>
    internal override void DrawRefreshQueueToBackbuffer(long tick)
    {
        // 0) If there’s no Scene (or no visible layers), just clear and publish the full frame.
        if (Scene == null || Scene.CountOfVisibleLayers == 0)
        {
            Backbuffer!.Canvas.Clear(Backbuffer.ClearColor);
            Backbuffer.MarkFullDirty();

            if (Scene != null)
                Scene.FullRefreshNeeded = false;

            _lastTick = tick;
            return;
        }

        // 1) find total real seconds passed since last background loop
        var deltaSeconds = HighResTimer.GetDuration(_lastTick, tick);

        // Are we in a "force full redraw" situation (camera moved, zoom changed, etc.)?
        bool forceFullRedraw = Scene!.FullRefreshNeeded;

        // 2) Handle full scene refresh once: clear and mark all layers as dirty.
        //    This already clears the whole backbuffer and enqueues a full rect per layer.
        if (forceFullRedraw)
            EnqueueFullSceneRefresh();

        // 3) Convert overlay (aka DirectDrawing) SCREEN dirty → WORLD dirty (queues)
        ProcessOverlayScreenDirty();

        // 4) Fast “no work” probe: no overlay dirty, no layer queues pending.
        if (!forceFullRedraw && !Scene.IsDirty)
        {
            _lastTick = tick; // keep deltaSeconds sane next frame
            return;
        }

        // 5) Render all views to Backbuffer. Draw layers back -> front (ascending Z).
        ViewRenderer.Render(Backbuffer!.Canvas, deltaSeconds, Scene!, (view, layer) =>
        {
            var refreshQueue = layer.RefreshQueue;

            // If this layer has no dirty regions and we are not forcing a full redraw, skip it.
            if (!forceFullRedraw && !refreshQueue.IsDirty)
                return;

            // When forceFullRedraw is true, EnqueueFullSceneRefresh should have
            // already pushed a full-world rect into each layer's queue, so we can
            // just use the rect list uniformly.
            foreach (var worldRect in refreshQueue.WorldRects)
            {
                // 1) Project world → screen for this view/layer
                var screenRectF = view.WorldRectToScreenRect(layer, worldRect);
                var screenRect = screenRectF.ToPixelAlignedRect();

                if (screenRect.Width <= 0 || screenRect.Height <= 0)
                    continue;

                // 2) Tell the backbuffer / adapter that this screen patch is dirty.
                Backbuffer.AddToDirtyRectangle(screenRect);

                // 3) Clip for perf and redraw just the tiles that intersect this world rect.
                Backbuffer.Canvas.Save();
                Backbuffer.Canvas.ClipRect(screenRect.ToSKRect());

                var tiles = layer.GetTilesInWorldRect(worldRect);

                Backbuffer.DrawTiles(tiles);

                Backbuffer.Canvas.Restore();
            }
        });


        // 6) Preserve any pre-existing dirty (e.g., set earlier this frame) — union, don’t replace.
        //    If this was a full redraw, mark the entire backbuffer as dirty so the adapter blits all of it.
        if (forceFullRedraw)
            Backbuffer!.MarkFullDirty();

        // DEBUG: visualize the adapter dirty rect in magenta
        if (!Backbuffer.DirtyRectangle.IsEmpty)
        {
            using var debugPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                Color = new SKColor(255, 0, 255, 255) // magenta
            };

            Backbuffer.Canvas.DrawRect(Backbuffer.DirtyRectangle.ToSKRect(), debugPaint);
        }

        // 7) Clear layer queues now that we’ve consumed them (avoids re-drawing same tiles next frame)
        for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
            Scene.VisibleSceneLayers[i].RefreshQueue.ClearRefreshQueue();

        Scene.FullRefreshNeeded = false;
        _lastTick = tick;
    }

    #region DrawRefreshQueueToBackbuffer helpers

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

        float zoom = viewport.Zoom;
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

            layer.RefreshQueue.AddWorldRect(worldRectInt);
        }
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
        if (RenderSurfaceAdapter is null)
            return;

        Backbuffer!.EndFrame();

        if (RedrawDirtyRectangleOnly)
            RenderBackbufferRect();
        else
            RenderBackbufferAll();

        Backbuffer.ClearDirtyRectangle();
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
            // If currently bound to a scene, unregister to avoid dangling references
            if (_scene != null)
            {
                try
                {
                    _scene.UnregisterRenderSurfaceHost(this);
                }
                catch
                {
                    // ignore errors during shutdown
                }
            }

            _backbuffer = null;
        }

        _disposed = true;
    }

    ~RenderSurfaceHost() => Dispose(false);

    #endregion IDisposable

    #region private methods

    private void OnSourceDisposing(Scene scene)
    {
        // Scene is being disposed — make sure we unregister and drop our reference
        try
        {
            scene.UnregisterRenderSurfaceHost(this);
        }
        catch
        {
            // swallow; defensive if scene is partially torn down
        }

        _scene = null;
    }

    private void OnRenderSurfaceAdapterResized(RenderSurfaceAdapterResizedEventArgs args)
    {
        if (Scene != null)
            Scene.FullRefreshNeeded = true;                 // full redraw next frame

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

    private void ProcessOverlayScreenDirty()
    {
        if (_overlayScreenDirty.IsEmpty)
            return;

        var screenDirty = _overlayScreenDirty;
        _overlayScreenDirty = Rectangle.Empty;

        if (Backbuffer is not null)
        {
            var canvas = Backbuffer.Canvas;

            // Match whatever the backbuffer uses as its clear color
            _overlayClearPaint.Color = Backbuffer.ClearColor;

            var skRect = screenDirty.ToSKRect();

            canvas.Save();
            canvas.ClipRect(skRect);
            canvas.DrawRect(skRect, _overlayClearPaint); // fills just this area
            canvas.Restore();

            // Make sure the adapter copies this region out AND that
            // DirectDrawingManager sees it as dirty for overlays.
            Backbuffer.AddToDirtyRectangle(screenDirty);
        }

        var scene = Scene!;
        foreach (var view in ViewRenderer.Views)
        {
            EnqueueOverlayWorldDirtyForView(view, scene, screenDirty);
        }
    }

    private void EnqueueOverlayWorldDirtyForView(View v, Scene scene, Rectangle screenDirty)
    {
        var cam = v.Camera;
        var vp = v.Viewport;
        float zoom = (vp.Zoom <= 0f) ? 1e-6f : vp.Zoom;

        float offsetX = vp.TargetRectPx.Left + vp.ScreenOffsetPx.X;
        float offsetY = vp.TargetRectPx.Top + vp.ScreenOffsetPx.Y;

        float localLeft = screenDirty.Left - offsetX;
        float localTop = screenDirty.Top - offsetY;
        float localWidth = screenDirty.Width;
        float localHeight = screenDirty.Height;

        for (int i = 0; i < scene.CountOfVisibleLayers; i++)
        {
            var layer = scene.VisibleSceneLayers[i];
            float p = layer.Parallax;

            // invert the render path:
            // screen = offset + (world - cam*p) / zoom
            // world  = cam*p + (screen - offset) * zoom
            float worldLeft = cam.PositionPx.X * p + localLeft * zoom;
            float worldTop = cam.PositionPx.Y * p + localTop * zoom;
            float worldWidth = localWidth * zoom;
            float worldHeight = localHeight * zoom;

            var worldRect = Rectangle.Round(
                new RectangleF(worldLeft, worldTop, worldWidth, worldHeight));

            layer.RefreshQueue.AddWorldRect(worldRect);
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