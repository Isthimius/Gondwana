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

    private readonly SKPaint _preClearPaint = new()
    {
        IsAntialias = false,
        BlendMode = SKBlendMode.Src,
        FilterQuality = SKFilterQuality.None
    };

    private readonly RenderSurfaceAdapterBase? _renderSurfaceAdapter;
    private readonly ViewRenderer _viewRenderer;

    public event EventHandler<RenderSurfaceHostBindEventArgs>? BindToScene;

    private RenderSurfaceHost() : base() => _viewRenderer = new ViewRenderer(this);

    public RenderSurfaceHost(RenderSurfaceAdapterBase renderSurfaceAdapter) : this()
    {
        _renderSurfaceAdapter = renderSurfaceAdapter ?? throw new ArgumentNullException(nameof(renderSurfaceAdapter));

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

        // 4.5) if doing a partial redraw, identify and clear any rects dirty from DirectDrawing overlays or dirty Tiles
        if (!forceFullRedraw)
        {
            var dirtyScreenRects = CollectDirtyScreenArea();

            // NEW: ensure overlay views get redrawn anywhere the screen is being repainted
            PropagateScreenDirtyToAllViews(dirtyScreenRects);

            PreclearScreenAreas(dirtyScreenRects);
        }

        // 5) Render all views to Backbuffer. Draw layers back -> front (ascending Z).
        ViewRenderer.Render(Backbuffer!.Canvas, deltaSeconds, Scene!,
            (view, layer) => RenderLayerDirtyRegions(view, layer, forceFullRedraw));

        // 6) Preserve any pre-existing dirty (e.g., set earlier this frame) — union, don’t replace.
        //    If this was a full redraw, mark the entire backbuffer as dirty so the adapter blits all of it.
        if (forceFullRedraw)
            Backbuffer!.MarkFullDirty();

        // 7) Clear layer queues now that we’ve consumed them (avoids re-drawing same tiles next frame)
        for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
            Scene.VisibleSceneLayers[i].RefreshQueue.ClearRefreshQueue();

        Scene.FullRefreshNeeded = false;
        _lastTick = tick;
    }

    #region DrawRefreshQueueToBackbuffer helpers

    private void EnqueueFullSceneRefresh()
    {
        if (Backbuffer is null || Scene is null)
            return;

        Backbuffer.Canvas.Clear(Backbuffer.ClearColor);

        foreach (var view in ViewRenderer.Views)
        {
            var viewport = view.Viewport;
            var screenRect = viewport.TargetRectPx;

            for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
            {
                var layer = Scene.VisibleSceneLayers[i];

                // 1) Compute layer-specific visible world rect
                var layerWorldRect = view.ScreenRectToWorldRect(layer, screenRect);

                // 2) Expand world rect by **one full tile** in all directions.
                //    This compensates for fractional-layer motion (parallax),
                //    camera motion, and tile boundaries.
                int expandX = layer.SceneLayerTileWidth;
                int expandY = layer.SceneLayerTileHeight;

                layerWorldRect.Inflate(expandX, expandY);

                // 3) Round to ints and enqueue
                var worldRectInt = layerWorldRect.ToPixelAlignedRect();

                layer.RefreshQueue.AddWorldRect(worldRectInt);
            }
        }
    }

    private void ProcessOverlayScreenDirty()
    {
        if (_overlayScreenDirty.IsEmpty)
            return;

        Backbuffer?.AddToDirtyRectangle(_overlayScreenDirty);

        // the dirty region is SCREEN rect
        RectangleF screenRect = _overlayScreenDirty;

        // find all SceneLayerTiles affected by the overlay dirty region
        foreach (var view in ViewRenderer.Views)
        {
            for (int i = 0; i < Scene!.CountOfVisibleLayers; i++)
            {
                var layer = Scene.VisibleSceneLayers[i];

                // use the canonical conversion logic in View to find WORLD rect
                var worldRectF = view.ScreenRectToWorldRect(layer, screenRect);
                worldRectF.Inflate(1, 1);

                // round back to int rect
                var worldRect = worldRectF.ToPixelAlignedRect();

                // add the affected tiles to the layer's refresh queue
                layer.RefreshQueue.AddWorldRect(worldRect);
            }
        }

        _overlayScreenDirty = Rectangle.Empty;
    }

    private List<Rectangle> CollectDirtyScreenArea()
    {
        var dirty = new List<Rectangle>(64);

        foreach (var view in ViewRenderer.Views)
        {
            var viewportRect = view.Viewport.TargetRectPx;

            foreach (var sceneLayer in Scene!.VisibleSceneLayers)
            {
                var refreshQueue = sceneLayer.RefreshQueue;
                if (!refreshQueue.IsDirty)
                    continue;

                foreach (var worldRect in refreshQueue.WorldRects)
                {
                    var screenRectF = view.WorldRectToScreenRect(sceneLayer, worldRect);
                    var rect = Rectangle.Intersect(
                        screenRectF.ToPixelAlignedRect(),
                        viewportRect);

                    if (rect.IsEmpty)
                        continue;

                    AddDeduped(rect, dirty);
                }
            }
        }

        return dirty;
    }

    private static void AddDeduped(Rectangle rect, List<Rectangle> list)
    {
        // If an existing rect fully contains this one, skip it
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Contains(rect))
                return;
        }

        // Merge with any overlapping rects
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (rect.IntersectsWith(list[i]))
            {
                rect = Rectangle.Union(rect, list[i]);
                list.RemoveAt(i);
            }
        }

        list.Add(rect);
    }

    // NEW: if base view dirties pixels under an overlay view, force overlay to redraw those pixels
    private void PropagateScreenDirtyToAllViews(List<Rectangle> dirtyScreenRects)
    {
        if (dirtyScreenRects == null || dirtyScreenRects.Count == 0 || Scene is null)
            return;

        foreach (var view in ViewRenderer.Views)
        {
            var vp = view.Viewport.TargetRectPx;

            foreach (var r in dirtyScreenRects)
            {
                if (!r.IntersectsWith(vp))
                    continue;

                // Overlay view? repaint the whole viewport
                var repaintRect = (view.ZOrder > 0)
                    ? vp
                    : Rectangle.Intersect(r, vp);

                var screenRectF = new RectangleF(repaintRect.Left, repaintRect.Top, repaintRect.Width, repaintRect.Height);

                for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
                {
                    var layer = Scene.VisibleSceneLayers[i];
                    var worldRectF = view.ScreenRectToWorldRect(layer, screenRectF);
                    worldRectF.Inflate(1, 1);
                    layer.RefreshQueue.AddWorldRect(worldRectF.ToPixelAlignedRect());
                }
            }
        }
    }

    private void PreclearScreenAreas(List<Rectangle> screenRects)
    {
        if (Backbuffer is null || screenRects is null || screenRects.Count == 0)
            return;

        var canvas = Backbuffer.Canvas;

        // Screen-pixel space
        canvas.Save();
        canvas.ResetMatrix();

        foreach (var r in screenRects)
        {
            if (r.IsEmpty || r.Width <= 0 || r.Height <= 0)
                continue;

            // Clear just this patch (overwrite with Backbuffer.ClearColor)
            Backbuffer.ClearRect(r);

            // Ensure adapter blits it
            r.Inflate(1, 1);
            Backbuffer.AddToDirtyRectangle(r);
        }

        canvas.Restore();
    }

    private void RenderLayerDirtyRegions(View view, SceneLayer layer, bool forceFullRedraw)
    {
        var refreshQueue = layer.RefreshQueue;

        // if this layer has no dirty regions and we are not forcing a full redraw, skip it.
        if (!forceFullRedraw && !refreshQueue.IsDirty)
            return;

        foreach (var worldRect in refreshQueue.WorldRects)
        {
            // 1) project world → screen for adapter dirty
            var screenRect = Rectangle.Intersect(
                view.WorldRectToScreenRect(layer, worldRect).ToPixelAlignedRect(),
                view.Viewport.TargetRectPx);

            if (screenRect.Width <= 0 || screenRect.Height <= 0)
                continue;

            // 2) mark adapter dirty (screen-space)
            Backbuffer!.AddToDirtyRectangle(screenRect);

            // 3) clip and redraw tiles (world-space)
            var canvas = Backbuffer.Canvas;

            canvas.Save();
            canvas.ClipRect(worldRect.ToSKRect());

            var tiles = layer.GetTilesInWorldRect(worldRect);
            Backbuffer.DrawTiles(tiles);

            canvas.Restore();
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

        var dirty = Backbuffer.DirtyRectangle;
        if (dirty.IsEmpty)
            return;

        var bounds = new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height);
        var clamped = Rectangle.Intersect(dirty, bounds);
        if (clamped.IsEmpty)
            return;

        var img = Backbuffer.Snapshot();

        // Post to UI thread
        Engine.Instance.UiDispatcher!.Post(() => RenderSurfaceAdapter!.Render(img, dirty.ToSKRectI(), dirty.ToSKRect()));
    }

    #endregion private methods
}