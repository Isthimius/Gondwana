using System.Drawing;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;

namespace Gondwana.Rendering;

internal sealed class RefreshQueue
{
    private readonly List<Rectangle> _worldRects;   // World-space dirty regions (pixels)
    private readonly object _syncRoot = new();      // Guards _worldRects for cross-thread access

    internal RefreshQueue() => _worldRects = new List<Rectangle>(64);

    /// <summary>
    /// True if there is at least one world-space dirty rectangle enqueued.
    /// </summary>
    internal bool IsDirty
    {
        get { lock (_syncRoot) return _worldRects.Count > 0; }
    }

    /// <summary>
    /// The queued world-space dirty rectangles (in pixels).
    /// These are consumed by the render path, which is responsible for:
    /// - Mapping them to tiles (SceneLayer / CoordinateSystem)
    /// - Mapping them to screen-space per view (View / RenderSurfaceHost)
    /// </summary>
    /// <remarks>
    /// Only safe to enumerate from the same thread that calls <see cref="AddWorldRect"/> and
    /// <see cref="ClearRefreshQueue"/>.  Call <see cref="SnapshotWorldRects"/> instead when
    /// iterating from a thread other than the engine thread (e.g. the GL paint thread).
    /// </remarks>
    internal IReadOnlyList<Rectangle> WorldRects => _worldRects;

    /// <summary>
    /// Returns a thread-safe point-in-time copy of the current dirty rectangles.
    /// Safe to call from any thread, including the GL paint thread.
    /// </summary>
    internal Rectangle[] SnapshotWorldRects()
    {
        lock (_syncRoot) return _worldRects.ToArray();
    }

    /// <summary>
    /// Enqueue a world-space pixel rectangle that requires redraw.
    /// Optionally cascades a notification to listeners (e.g., other hosts).
    /// ***** IMPORTANT: must ALWAYS be in WORLD pixels. *****
    /// </summary>
    internal void AddWorldRect(Rectangle worldPixelRange)
    {
        if (worldPixelRange.IsEmpty)
            return;

        // ensure we're on the engine thread
        var engine = Engine.Instance;
        if (!engine.EngineDispatcher.IsOnEngineThread)
        {
            engine.EngineDispatcher.Post(() => AddWorldRect(worldPixelRange));
            return;
        }

        lock (_syncRoot)
        {
            // Fast containment check: if any existing rect already fully contains this one, skip storing it.
            for (int i = 0; i < _worldRects.Count; i++)
            {
                if (_worldRects[i].Contains(worldPixelRange))
                    return;
            }

            _worldRects.Add(worldPixelRange);
        }
    }

    internal void AddViewScreenRect(View view, SceneLayer sceneLayer, Rectangle screenPixelRange)
    {
        if (screenPixelRange.IsEmpty)
            return;
        
        if (view is null)
            throw new ArgumentNullException(nameof(view));
        
        if (sceneLayer is null)
            throw new ArgumentNullException(nameof(sceneLayer));

        // ensure we're on the engine thread
        var engine = Engine.Instance;
        if (!engine.EngineDispatcher.IsOnEngineThread)
        {
            engine.EngineDispatcher.Post(() => AddViewScreenRect(view, sceneLayer, screenPixelRange));
            return;
        }

        // clamp screenPixelRange to view's viewport
        screenPixelRange.Intersect(view.Viewport.TargetRectPx);

        var worldRect = view.ScreenRectToWorldRect(sceneLayer, screenPixelRange);
        worldRect.Inflate(3, 3); // Expand by 1 pixel in all directions to account for rounding errors.
        AddWorldRect(worldRect.ToPixelAlignedRect());
    }

    internal void ClearRefreshQueue()
    {
        // ensure we're on the engine thread
        var engine = Engine.Instance;
        if (!engine.EngineDispatcher.IsOnEngineThread)
        {
            engine.EngineDispatcher.Post(() => ClearRefreshQueue());
            return;
        }

        lock (_syncRoot)
            _worldRects.Clear();
    }
}
