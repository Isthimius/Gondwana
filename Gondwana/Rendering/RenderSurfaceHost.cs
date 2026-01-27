using Gondwana.Drawing.Direct;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using SkiaSharp;
using System.Drawing;

namespace Gondwana.Rendering;

/// <summary>
/// Hosts a render surface and manages rendering of a scene to a backbuffer of the specified type. Provides coordination
/// between the scene, views, and the underlying UI adapter for efficient rendering and presentation.
/// </summary>
/// <remarks>RenderSurfaceHost<TBackbuffer> is responsible for managing the lifecycle of the backbuffer, handling
/// scene binding, and coordinating redraws based on scene and view changes. It supports partial or full redraws
/// depending on the state of the scene and the RedrawDirtyRectangleOnly property. Thread safety
/// is not guaranteed; all interactions should occur on the UI thread associated with the render surface
/// adapter.</remarks>
/// <typeparam name="TBackbuffer">The type of backbuffer used for rendering. Must inherit from BackbufferBase.</typeparam>
public sealed class RenderSurfaceHost<TBackbuffer> : RenderSurfaceHostBase
    where TBackbuffer : BackbufferBase
{
    private TBackbuffer _backbuffer;
    private Scene _scene = Scene.Empty;

    private readonly RenderSurfaceAdapterBase _renderSurfaceAdapter;
    private readonly ViewManager _viewManager;

    public event EventHandler<RenderSurfaceHostBindEventArgs>? BindToScene;

    private RenderSurfaceHost() : base() => _viewManager = new ViewManager(this);

    public RenderSurfaceHost(RenderSurfaceAdapterBase renderSurfaceAdapter) : this()
    {
        _renderSurfaceAdapter = renderSurfaceAdapter ?? throw new ArgumentNullException(nameof(renderSurfaceAdapter));

        // Recreate backbuffer on adapter resize
        RenderSurfaceAdapter.Resized += (args) => OnRenderSurfaceAdapterResized(args);

        var w = RenderSurfaceAdapter.Width;
        var h = RenderSurfaceAdapter.Height;

        if (w <= 0 || h <= 0)
            throw new InvalidOperationException("RenderSurfaceAdapter has non-positive dimensions.");

        _backbuffer = (TBackbuffer)Activator.CreateInstance(typeof(TBackbuffer), w, h)!;
        Backbuffer.BeginFrame();

        Backbuffer.SizeChanged += (w, h) => Scene.FullRefreshNeeded = true;
    }

    public override BackbufferBase Backbuffer => _backbuffer;

    public override Scene Scene => _scene;

    public override RenderSurfaceAdapterBase RenderSurfaceAdapter => _renderSurfaceAdapter;

    public override ViewManager ViewManager => _viewManager;

    public bool RedrawDirtyRectangleOnly { get; set; } = true;

    public void Bind(Scene newScene, bool limitCameraToWorldBoundPx = true)
    {
        if (newScene == null)
            throw new ArgumentNullException(nameof(newScene), "Cannot bind to null Scene.");

        if (newScene == _scene)
            return;

        // Unregister from the old scene (if any)
        if (_scene != null)
        {
            _scene.SceneDisposing -= OnSourceDisposing;
        }

        var oldScene = _scene;
        _scene = newScene;

        ViewManager.BindToScene(_scene, limitCameraToWorldBoundPx);
        _scene.SceneDisposing += OnSourceDisposing;
        _scene.FullRefreshNeeded = true;

        BindToScene?.Invoke(this, new RenderSurfaceHostBindEventArgs(oldScene, _scene));
    }

    /// <summary>
    /// Renders all visible scene layers for every configured view onto the backbuffer.
    /// Called as part of DoForegroundTasks().
    /// </summary>
    internal override void RenderToBackbuffer(long tick)
    {
        // 0) If there are no visible SceneLayers, just clear and publish the full frame.
        if (Scene.CountOfVisibleLayers == 0)
        {
            Backbuffer.ClearRect(new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height));
            Scene.FullRefreshNeeded = false;

            return;
        }

        // 1) Handle full scene refresh once (camera moved, zoom changed, etc.): clear and mark all layers as dirty.
        //    This already clears the whole backbuffer and enqueues a full rect per layer.
        if (Scene.FullRefreshNeeded)
        {
            // this will mark the Scene.IsDirty flag as true
            EnqueueFullSceneRefresh();
        }
        else
        {
            // scene is not dirty, this frame is done...
            if (!Scene.IsDirty)
                return;
        }

        // 3) Render all views to Backbuffer. Draw layers back -> front (ascending Z).
        foreach (var view in ViewManager.Views)
        {
            // 3.1) Force-refresh all DirectDrawings that overlay this view
            var overlays = DirectDrawingManager.Instance.GetDrawingsForView(view);
            foreach (var overlay in overlays)
            {
                overlay.ForceRefresh();
            }

            // 3.1) Clip to this view’s viewport
            var vp = view.Viewport.TargetRectPx;

            Backbuffer.Canvas.Save();
            Backbuffer.Canvas.ResetMatrix();
            Backbuffer.Canvas.ClipRect(vp.ToSKRect(), SKClipOperation.Intersect, antialias: false);

            // 2) identify dirty screen SCREEN areas across all views and layers
            var dirtyScreenRects = CollectDirtyScreenArea(view);

            // 3.2) Pre-clear dirty areas on backbuffer to Backbuffer.ClearColor
            PreclearScreenAreas(view, dirtyScreenRects);

            // 3.3) Render each visible layer’s dirty regions for this view
            //      this will draw SceneLayerTiles, Sprites, and SceneLayer-based DirectDrawings
            var sceneLayers = Scene.VisibleSceneLayers;

            for (int i = 0; i < sceneLayers.Count; i++)
            {
                var layer = sceneLayers[i];
                RenderLayerDirtyRegions(view, layer);
            }

            // 3.4) draw all View-based DirectDrawings for this view
            // TODO: limit this to dirty areas only
            //var overlays = DirectDrawingManager.Instance.GetDrawingsForView(view);
            for (int i = 0; i < overlays.Count; i++)
                overlays[i].Draw(Backbuffer, overlays[i].GetDrawLocationScreen(view));

            // 3.5) Restore from viewport clip
            Backbuffer.Canvas.Restore();
        }

        // 4) Clear layer queues now that we’ve consumed them (avoids re-drawing same tiles next frame)
        for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
            Scene.VisibleSceneLayers[i].RefreshQueue.ClearRefreshQueue();

        Scene.FullRefreshNeeded = false;
    }

    #region DrawRefreshQueueToBackbuffer helpers

    private void EnqueueFullSceneRefresh()
    {
        foreach (var view in ViewManager.Views)
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

    private List<Rectangle> CollectDirtyScreenArea(View view)
    {
        var dirty = new List<Rectangle>(64);
        var viewportRect = view.Viewport.TargetRectPx;

        foreach (var sceneLayer in Scene.VisibleSceneLayers)
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

                dirty.AddDeduped(rect);
            }
        }

        return dirty;
    }

    private void PreclearScreenAreas(View view, List<Rectangle> screenRects)
    {
        if (Backbuffer is null || screenRects is null || screenRects.Count == 0)
            return;

        foreach (var screenRect in screenRects)
        {
            var screenRectViewport = Rectangle.Intersect(screenRect, view.Viewport.TargetRectPx);

            if (screenRectViewport.IsEmpty || screenRectViewport.Width <= 0 || screenRectViewport.Height <= 0)
                continue;

            // Clear just this patch (overwrite with Backbuffer.ClearColor)
            Backbuffer.ClearRect(screenRectViewport);

            EnqueueForOverlappingViewsAtOrAbove(view, screenRectViewport);
        }
    }

    private void EnqueueForOverlappingViewsAtOrAbove(View sourceView, Rectangle clearedScreenRect)
    {
        var views = ViewManager.Views; // Z-ordered
        int srcIndex = ViewManager.Views.IndexOf(sourceView);
        if (srcIndex < 0)
            return;

        for (int j = srcIndex; j < views.Count; j++)
        {
            var v = views[j];
            var overlap = Rectangle.Intersect(clearedScreenRect, v.Viewport.TargetRectPx);
            if (overlap.IsEmpty)
                continue;

            foreach (var sceneLayer in Scene.VisibleSceneLayers)
            {
                var worldRectF = v.ScreenRectToWorldRect(sceneLayer, overlap);
                var worldRect = worldRectF.ToPixelAlignedRect();

                if (!worldRect.IsEmpty)
                    sceneLayer.RefreshQueue.AddWorldRect(worldRect);
            }
        }
    }

    private void RenderLayerDirtyRegions(View view, SceneLayer layer)
    {
        var refreshQueue = layer.RefreshQueue;

        // if this layer has no dirty regions and we are not forcing a full redraw, skip it.
        if (!refreshQueue.IsDirty)
            return;

        foreach (var worldRect in refreshQueue.WorldRects)
        {
            // project world → screen for adapter dirty
            var screenRect = view.WorldRectToScreenRect(layer, worldRect).ToPixelAlignedRect();

            // clip to viewport
            var clipRect = Rectangle.Intersect(screenRect, view.Viewport.TargetRectPx);

            // exit it out of clip
            if (clipRect.Width <= 0 || clipRect.Height <= 0)
                continue;

            // draw tiles/sprites/direct drawings in this world rect
            var drawables = layer.GetDrawablesInWorldRect(worldRect);
            Backbuffer.DrawDrawables(view, drawables, clipRect);
        }
    }

    #endregion DrawRefreshQueueToBackbuffer helpers

    /// <summary>
    /// Renders the contents of the backbuffer to the associated UI adapter.
    /// Called as part of DoForegroundTasks().
    /// </summary>
    /// <remarks>This method finalizes the current frame on the backbuffer and renders its contents  to the
    /// adapter. If <see cref="RedrawDirtyRectangleOnly"/> is <see langword="true"/>, only the dirty rectangle is
    /// redrawn; otherwise, the entire backbuffer is rendered. After rendering, the dirty rectangle is reset, and the
    /// backbuffer is prepared for the next frame.</remarks>
    internal override void PresentBackbufferToAdapter()
    {
        if (RenderSurfaceAdapter is null)
            return;

        Backbuffer.EndFrame();

        if (RedrawDirtyRectangleOnly)
            PresentBackbufferRect();
        else
            PresentBackbufferAll();

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
            _backbuffer = null;
        }

        _disposed = true;
    }

    ~RenderSurfaceHost() => Dispose(false);

    #endregion IDisposable

    #region private methods

    private void OnSourceDisposing(Scene scene)
    {
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
        foreach (var view in ViewManager.Views)
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

    private void PresentBackbufferAll()
    {
        if (Backbuffer == null)
            return;

        var img = Backbuffer.Snapshot();
        var src = new SKRectI(0, 0, img.Width, img.Height);
        var dst = SKRect.Create(0, 0, RenderSurfaceAdapter!.Width, RenderSurfaceAdapter.Height);

        // Post to UI thread
        Engine.Instance.UiDispatcher!.Post(() => RenderSurfaceAdapter.Present(img, src, dst));
    }

    private void PresentBackbufferRect()
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
        Engine.Instance.UiDispatcher!.Post(() => RenderSurfaceAdapter!.Present(img, dirty.ToSKRectI(), dirty.ToSKRect()));
    }

    #endregion private methods
}