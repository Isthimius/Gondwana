using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Rendering;

/// <summary>
/// Represents a queue for managing refresh operations within a SceneLayer.
/// Tracks world-space pixel areas that need redraw. It does NOT resolve to
/// tiles or sprites; that is the responsibility of SceneLayer / render code.
/// </summary>
internal sealed class RefreshQueue : IDisposable
{
    private readonly List<Rectangle> _worldRects;   // World-space dirty regions (pixels)
    private readonly SceneLayer _sceneLayer;        // Parent layer (for event context)

    internal RefreshQueue(SceneLayer layer)
    {
        _sceneLayer = layer ?? throw new ArgumentNullException(nameof(layer));
        _worldRects = new List<Rectangle>(64);
    }

    ~RefreshQueue() => Dispose();

    /// <summary>
    /// True if there is at least one world-space dirty rectangle enqueued.
    /// </summary>
    internal bool IsDirty => _worldRects.Count > 0;

    /// <summary>
    /// The queued world-space dirty rectangles (in pixels).
    /// These are consumed by the render path, which is responsible for:
    /// - Mapping them to tiles (SceneLayer / CoordinateSystem)
    /// - Mapping them to screen-space per view (View / RenderSurfaceHost)
    /// </summary>
    internal IReadOnlyList<Rectangle> WorldRects => _worldRects;

    /// <summary>
    /// Enqueue a world-space pixel rectangle that requires redraw.
    /// Optionally cascades a notification to listeners (e.g., other hosts).
    /// ***** IMPORTANT: must ALWAYS be in WORLD pixels. *****
    /// </summary>
    internal void AddWorldRect(Rectangle worldPixelRange)
    {
        if (worldPixelRange.IsEmpty)
            return;

        // Normalize any negative-width/height rectangles (paranoia).
        var normalized = Rectangle.FromLTRB(
            worldPixelRange.Left,
            worldPixelRange.Top,
            worldPixelRange.Right,
            worldPixelRange.Bottom);

        // Fast containment check: if any existing rect already fully contains this one, skip storing it.
        for (int i = 0; i < _worldRects.Count; i++)
        {
            if (_worldRects[i].Contains(normalized))
                return;
        }

        _worldRects.Add(normalized);
    }

    /// <summary>
    /// Clears all queued world-space refresh regions.
    /// </summary>
    internal void ClearRefreshQueue()
    {
        _worldRects.Clear();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
