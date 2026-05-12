using Gondwana.Drawing.Direct;
using Gondwana.Extensibility;
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

    /// <summary>
    /// Occurs when a scene is bound to or unbound from this render surface host.
    /// </summary>
    /// <remarks>
    /// This event fires after the scene binding operation completes. Event handlers receive information
    /// about the previously bound scene (if any) and the newly bound scene. Use this event to respond
    /// to scene changes, such as updating UI elements or resetting state that depends on the active scene.
    /// </remarks>
    public event EventHandler<RenderSurfaceHostBindEventArgs>? BindToScene;

    /// <summary>
    /// Occurs at the beginning of the backbuffer rendering process.
    /// </summary>
    public event Action? RenderBackbufferBegin;

    /// <summary>
    /// Occurs at the end of the backbuffer rendering process.
    /// </summary>
    public event Action? RenderBackbufferEnd;

    /// <summary>
    /// Occurs when a backbuffer render operation is skipped because the scene is not dirty.
    /// </summary>
    public event Action? RenderBackbufferNoOp;

    /// <summary>
    /// Occurs after all scene content (layers, sprites, and direct drawings) has been drawn to the
    /// backbuffer canvas for the current frame, but before the frame is finalised and presented to
    /// the display adapter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Subscribe to this event to draw post-scene overlays or visual effects — such as color-grading
    /// filters, vignettes, bloom composites, or debug annotations — directly onto the fully-rendered
    /// canvas.  The canvas matrix is reset to identity and the clip covers the full surface at the
    /// point this event fires.  Save and restore canvas state around any operations that modify the
    /// matrix or clip region.
    /// </para>
    /// <para>
    /// <strong>This is the per-surface game-instance hook.</strong>  If you need the same capability
    /// from an engine-wide plugin, implement <see cref="IEnginePlugin.OnPostRenderCanvas"/> instead.
    /// </para>
    /// <para>
    /// Threading:
    /// <list type="bullet">
    /// <item><description>
    ///   <strong>CPU/bitmap surfaces</strong> — raised on the engine background thread.
    /// </description></item>
    /// <item><description>
    ///   <strong>GPU/GL surfaces</strong> — raised on the GL thread from within
    ///   <c>PaintSurface</c>, while the <c>GRContext</c> is current.
    ///   Do not marshal GPU canvas operations to a different thread.
    /// </description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This event is not raised when a frame is skipped (scene not dirty) or when the surface has
    /// no configured views.
    /// </para>
    /// </remarks>
    public event Action<SKCanvas>? RenderBackbufferPostScene;

    private RenderSurfaceHost() : base() => _viewManager = new ViewManager(this);

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderSurfaceHost{TBackbuffer}"/> class with the specified render surface adapter.
    /// </summary>
    /// <param name="renderSurfaceAdapter">The platform-specific adapter that provides the underlying rendering surface.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="renderSurfaceAdapter"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the adapter has non-positive dimensions.</exception>
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

    /// <summary>
    /// Gets the backbuffer used for off-screen rendering before presentation to the adapter.
    /// </summary>
    /// <value>
    /// The <see cref="BackbufferBase"/> instance managing the render target canvas and dirty-rectangle tracking.
    /// </value>
    /// <remarks>
    /// The backbuffer holds the rendered frame content and tracks which regions have changed since the last
    /// presentation. All rendering operations are performed on the backbuffer's canvas before being
    /// copied to the UI adapter during presentation.
    /// </remarks>
    public override BackbufferBase Backbuffer => _backbuffer;

    /// <summary>
    /// Gets the scene currently bound to this render surface host.
    /// </summary>
    /// <value>
    /// The <see cref="Scene"/> instance being rendered, or <see cref="Scene.Empty"/> if no scene is bound.
    /// </value>
    /// <remarks>
    /// The scene contains all layers, sprites, and direct drawings that are rendered each frame.
    /// Use <see cref="Bind"/> to change the active scene.
    /// </remarks>
    public override Scene Scene => _scene;

    /// <summary>
    /// Gets the platform-specific adapter that provides the underlying rendering surface.
    /// </summary>
    /// <value>
    /// The <see cref="RenderSurfaceAdapterBase"/> instance representing the UI control or window
    /// where rendered content is displayed.
    /// </value>
    /// <remarks>
    /// The adapter handles platform-specific presentation details and provides size/resize notifications.
    /// The backbuffer dimensions are synchronized with the adapter's size.
    /// </remarks>
    public override RenderSurfaceAdapterBase RenderSurfaceAdapter => _renderSurfaceAdapter;

    /// <summary>
    /// Gets the view manager that controls camera positions, viewports, and multi-view rendering.
    /// </summary>
    /// <value>
    /// The <see cref="ViewManager"/> instance managing all views associated with this render surface host.
    /// </value>
    /// <remarks>
    /// Use the view manager to create, configure, and remove views. Each view defines a viewport
    /// rectangle on the backbuffer, a camera position in the scene, and zoom/parallax settings.
    /// Views enable split-screen, picture-in-picture, and minimap rendering.
    /// </remarks>
    public override ViewManager ViewManager => _viewManager;

    /// <summary>
    /// Gets or sets a value indicating whether only the dirty (changed) regions of the backbuffer
    /// are presented to the adapter each frame.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to present only dirty regions (default); <see langword="false"/> to
    /// present the entire backbuffer every frame.
    /// </value>
    /// <remarks>
    /// Enabling dirty-rectangle-only presentation improves performance by reducing the amount of
    /// data transferred to the UI adapter. Disable this when troubleshooting rendering issues or
    /// when the adapter does not support partial updates.
    /// </remarks>
    public bool RedrawDirtyRectangleOnly { get; set; } = true;

    /// <summary>
    /// Binds a scene to this render surface host, replacing any previously bound scene.
    /// </summary>
    /// <param name="newScene">The scene to bind. Must not be <see langword="null"/>.</param>
    /// <param name="limitCameraToWorldBoundPx">
    /// <see langword="true"/> to constrain all view cameras to the scene's world bounds;
    /// <see langword="false"/> to allow cameras to move freely beyond the scene boundaries.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="newScene"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Binding a scene unregisters event handlers from the previous scene (if any), registers handlers
    /// with the new scene, and triggers a full refresh on the next frame. The <see cref="BindToScene"/>
    /// event fires after the binding operation completes.
    /// </para>
    /// <para>
    /// If the specified scene is already bound, this method returns immediately without performing
    /// any operations.
    /// </para>
    /// </remarks>
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
        RenderBackbufferBegin?.Invoke();

        // For GL-thread-rendered backbuffers (GpuBackbuffer), the RefreshQueue mechanism is
        // unreliable: AddWorldRect and ClearRefreshQueue both post to the engine thread, so
        // enqueued rects are not present when CollectDirtyScreenArea runs, and posted clears
        // can silently discard dirty rects that were added after the last GL frame.
        // Instead, always re-render the entire surface each GL paint callback.
        if (Backbuffer.IsGlThreadRendered)
        {
            RenderToBackbufferGpuFull(tick);
            RenderBackbufferEnd?.Invoke();
            return;
        }

        // 0) If there are no visible SceneLayers, just clear and publish the full frame.
        if (Scene.CountOfVisibleLayers == 0)
        {
            Backbuffer.ClearRect(new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height));
            Scene.FullRefreshNeeded = false;
        }
        else
        {
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
                {
                    RenderBackbufferNoOp?.Invoke();
                    return;
                }
            }
        }

        // 2) Render all views to Backbuffer. Draw layers back -> front (ascending Z).
        foreach (var view in ViewManager.Views)
        {
            RenderContext.Push(view, tick);

            try
            {
                // 2.1) Force-refresh all DirectDrawings that overlay this view
                var overlays = DirectDrawingManager.Instance.GetDrawingsForView(view);
                foreach (var overlay in overlays)
                {
                    overlay.ForceRefresh();
                }

                // 2.2) identify dirty SCREEN areas across all layers for this view
                var dirtyScreenRects = CollectDirtyScreenArea(view);

                // 2.3) Clip to this view's viewport
                var vp = view.Viewport.TargetRectPx;

                // clip inclusive to current viewport
                Backbuffer.Canvas.Save();
                Backbuffer.Canvas.ResetMatrix();
                Backbuffer.Canvas.ClipRect(vp.ToSKRect(), SKClipOperation.Intersect, antialias: false);

                // clip exclusive to higher Z-order views
                foreach (var blocker in ViewManager.GetViewsAbove(view))
                {
                    var overlap = Rectangle.Intersect(vp, blocker.Viewport.TargetRectPx);
                    if (!overlap.IsEmpty)
                        Backbuffer.Canvas.ClipRect(overlap.ToSKRect(), SKClipOperation.Difference, antialias: false);
                }

                // 2.4) Pre-clear dirty areas on backbuffer to Backbuffer.ClearColor
                PreclearScreenAreas(view, dirtyScreenRects);

                // 2.5) Render each visible layer's dirty regions for this view
                //      this will draw SceneLayerTiles, Sprites, and SceneLayer-based DirectDrawings
                var sceneLayers = Scene.VisibleSceneLayers;

                for (int i = 0; i < sceneLayers.Count; i++)
                {
                    var layer = sceneLayers[i];
                    RenderLayerDirtyRegions(view, layer);
                }

                // 2.6) draw all View-based DirectDrawings for this view
                for (int i = 0; i < overlays.Count; i++)
                    overlays[i].Draw(Backbuffer, overlays[i].GetDrawLocationScreen(view));

                // 2.7) Restore from viewport clip
                Backbuffer.Canvas.Restore();
            }
            finally
            {
                RenderContext.Pop();
            }
        }

        // 3) Clear layer queues now that we've consumed them (avoids re-drawing same tiles next frame)
        for (int i = 0; i < Scene.CountOfVisibleLayers; i++)
            Scene.VisibleSceneLayers[i].RefreshQueue.ClearRefreshQueue();

        Scene.FullRefreshNeeded = false;

        // Notify subscribers that all scene content has been drawn and the canvas is ready for
        // post-scene effects.  Fires before RenderBackbufferEnd so subscribers still have a
        // chance to draw into the canvas before it is presented to the adapter.
        RenderBackbufferPostScene?.Invoke(Backbuffer.Canvas);
        EnginePluginRegistry.InvokePostRenderCanvas(Engine.Instance, this, Backbuffer.Canvas);

        RenderBackbufferEnd?.Invoke();
    }

    /// <summary>
    /// Full-surface rendering path used exclusively for GL-thread-rendered backbuffers
    /// (i.e. <see cref="GpuBackbuffer"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The standard dirty-rectangle path (<see cref="EnqueueFullSceneRefresh"/>,
    /// <see cref="CollectDirtyScreenArea"/>, and <see cref="RefreshQueue.ClearRefreshQueue"/>)
    /// relies on posting work items to the engine thread.  When
    /// <see cref="BackbufferBase.IsGlThreadRendered"/> is <see langword="true"/> this posting
    /// causes two races:
    /// </para>
    /// <list type="number">
    /// <item><description>
    ///   Full-refresh world rects enqueued by <see cref="EnqueueFullSceneRefresh"/> are not yet
    ///   present in the queue when <see cref="CollectDirtyScreenArea"/> runs in the same GL frame,
    ///   so large regions are silently skipped.
    /// </description></item>
    /// <item><description>
    ///   The posted <see cref="RefreshQueue.ClearRefreshQueue"/> may execute on the engine thread
    ///   after new dirty rects have been added by game logic, wiping those rects before the next
    ///   GL frame can render them.
    /// </description></item>
    /// </list>
    /// <para>
    /// This method bypasses the <see cref="RefreshQueue"/> entirely: it clears and re-draws the
    /// full viewport on every GL paint callback, which is correct because the GL paint fires once
    /// per vsync and there is no partial-blit optimisation to preserve.
    /// </para>
    /// </remarks>
    private void RenderToBackbufferGpuFull(long tick)
    {
        // When there are no views at all, clear the whole surface and bail.
        // If there ARE views but no scene layers, we fall through so view-mode
        // DirectDrawings (e.g. a splash screen overlay) are still rendered.
        if (ViewManager.Views.Count == 0)
        {
            Backbuffer.ClearRect(new Rectangle(0, 0, Backbuffer.Width, Backbuffer.Height));
            Scene.FullRefreshNeeded = false;
            return;
        }

        foreach (var view in ViewManager.Views)
        {
            RenderContext.Push(view, tick);

            try
            {
                // 1) Force-refresh all DirectDrawings that overlay this view.
                var overlays = DirectDrawingManager.Instance.GetDrawingsForView(view);
                foreach (var overlay in overlays)
                    overlay.ForceRefresh();

                var vp = view.Viewport.TargetRectPx;

                // 2) Clip to this view's viewport, excluding areas covered by higher Z-order views.
                Backbuffer.Canvas.Save();
                Backbuffer.Canvas.ResetMatrix();
                Backbuffer.Canvas.ClipRect(vp.ToSKRect(), SKClipOperation.Intersect, antialias: false);

                foreach (var blocker in ViewManager.GetViewsAbove(view))
                {
                    var overlap = Rectangle.Intersect(vp, blocker.Viewport.TargetRectPx);
                    if (!overlap.IsEmpty)
                        Backbuffer.Canvas.ClipRect(overlap.ToSKRect(), SKClipOperation.Difference, antialias: false);
                }

                // 3) Clear the full viewport.
                Backbuffer.ClearRect(vp);

                // 4) Render every visible layer for the full viewport extent (layers are drawn
                //    back-to-front by ascending Z-order, which VisibleSceneLayers already provides).
                var sceneLayers = Scene.VisibleSceneLayers;

                for (int i = 0; i < sceneLayers.Count; i++)
                {
                    var layer = sceneLayers[i];

                    // Compute the world-space rect visible through this viewport for this layer,
                    // expanded by one tile in each direction to cover boundary rounding.
                    var layerWorldRectF = view.ScreenRectToWorldRect(layer, vp);
                    layerWorldRectF.Inflate(layer.TileWidth, layer.TileHeight);
                    var layerWorldRect = layerWorldRectF.ToPixelAlignedRect();

                    var drawables = layer.GetDrawablesInWorldRect(layerWorldRect);
                    Backbuffer.DrawDrawables(view, drawables, vp);
                }

                // 5) Render view-based DirectDrawings on top.
                for (int i = 0; i < overlays.Count; i++)
                    overlays[i].Draw(Backbuffer, overlays[i].GetDrawLocationScreen(view));

                Backbuffer.Canvas.Restore();
            }
            finally
            {
                RenderContext.Pop();
            }
        }

        Scene.FullRefreshNeeded = false;

        // Notify subscribers that all scene content has been drawn and the canvas is ready for
        // post-scene effects.  For GPU surfaces this runs on the GL thread while GRContext is
        // current, so subscribers may safely issue Skia GPU draw calls.
        RenderBackbufferPostScene?.Invoke(Backbuffer.Canvas);
        EnginePluginRegistry.InvokePostRenderCanvas(Engine.Instance, this, Backbuffer.Canvas);
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
                int expandX = layer.TileWidth;
                int expandY = layer.TileHeight;

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

            foreach (var worldRect in refreshQueue.SnapshotWorldRects())
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

            EnqueueForOverlappingSceneLayers(view, screenRectViewport);
        }
    }

    private void EnqueueForOverlappingSceneLayers(View sourceView, Rectangle clearedScreenRect)
    {
        var overlap = Rectangle.Intersect(clearedScreenRect, sourceView.Viewport.TargetRectPx);

        if (overlap.IsEmpty)
            return;

        foreach (var sceneLayer in Scene.VisibleSceneLayers)
        {
            var worldRectF = sourceView.ScreenRectToWorldRect(sceneLayer, overlap);
            var worldRect = worldRectF.ToPixelAlignedRect();

            if (!worldRect.IsEmpty)
                sceneLayer.RefreshQueue.AddWorldRect(worldRect);
        }
    }

    private void RenderLayerDirtyRegions(View view, SceneLayer layer)
    {
        var refreshQueue = layer.RefreshQueue;

        // if this layer has no dirty regions and we are not forcing a full redraw, skip it.
        if (!refreshQueue.IsDirty)
            return;

        foreach (var worldRect in refreshQueue.SnapshotWorldRects())
        {
            // draw tiles/sprites/direct drawings in this world rect
            var drawables = layer.GetDrawablesInWorldRect(worldRect);

            // project world → screen for adapter dirty
            var screenRect = view.WorldRectToScreenRect(layer, worldRect).ToPixelAlignedRect();

            Backbuffer.DrawDrawables(view, drawables, screenRect);
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

    /// <summary>
    /// Releases all resources used by this <see cref="RenderSurfaceHost{TBackbuffer}"/> instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method releases managed resources including the backbuffer. It does not unregister
    /// from the scene; that should be handled by the scene's disposal logic.
    /// </para>
    /// <para>
    /// After calling <see cref="Dispose()"/>, this instance should not be used. Calling
    /// <see cref="Dispose()"/> multiple times is safe and has no additional effect.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        Dispose(true);
    }

    /// <summary>
    /// Releases resources used by this <see cref="RenderSurfaceHost{TBackbuffer}"/> instance.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> to release both managed and unmanaged resources;
    /// <see langword="false"/> to release only unmanaged resources (called from finalizer).
    /// </param>
    /// <remarks>
    /// When <paramref name="disposing"/> is <see langword="true"/>, this method releases the backbuffer
    /// and clears the reference. Derived classes should override this method to release additional resources.
    /// </remarks>
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