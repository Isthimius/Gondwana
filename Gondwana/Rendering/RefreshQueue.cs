using Gondwana.Drawing;
using Gondwana.Drawing.Sprites;
using Gondwana.Scenes;
using System.Drawing;

namespace Gondwana.Rendering;

/// <summary>
/// Represents a queue for managing refresh operations within a SceneLayer.
/// Tracks pixel areas that need redraw and resolves them to tiles/sprites on demand.
/// </summary>
/// <remarks>
/// Optimizations:
/// - Uses a HashSet to de-duplicate tiles (O(1) membership) instead of O(n) IndexOf.
/// - Per-area refresh updates only the tiles hit by that area (avoids O(T) per area).
/// - Early exit when new rect is contained by an existing rect.
/// </remarks>
internal sealed class RefreshQueue : IDisposable
{
    // --- State ---
    private bool _isDirty;                       // If true, Tiles need to be recomputed from _rects.
    private readonly List<Tile> _tiles;          // Unique list of Tile objects to be redrawn.
    private readonly HashSet<Tile> _tileSet;     // O(1) membership check mirroring _tiles.
    private readonly List<Rectangle> _rects;     // Pixel areas requiring refresh.
    private readonly SceneLayer _sceneLayer;     // Parent layer (for coords and sprite queries).

    internal event Action<RefreshQueueAreaAddedEventArgs>? RefreshQueueAreaAdded;

    internal RefreshQueue(SceneLayer layer)
    {
        _isDirty = false;
        _tiles = new List<Tile>(256);
        _tileSet = new HashSet<Tile>();
        _rects = new List<Rectangle>(64);
        _sceneLayer = layer;
    }

    ~RefreshQueue() => Dispose();

    /// <summary>
    /// The tiles that must be redrawn this pass. Recomputed lazily when <see cref="_isDirty"/> is set.
    /// </summary>
    internal List<Tile> Tiles
    {
        get
        {
            if (_isDirty)
                FindTilesInRange();

            return _tiles;
        }
    }

    /// <summary>
    /// Enqueue a screen/world pixel rectangle that requires redraw.
    /// Optionally cascades a notification to sibling queues (e.g., other layers).
    /// ***** IMPORTANT: should ALWAYS be in Backbuffer WORLD pixels. *****
    /// </summary>
    internal void AddPixelRangeToRefreshQueue(Rectangle pixelRange, bool cascadeToOtherRefreshQueues)
    {
        if (pixelRange.IsEmpty) return;

        // Cascade to other queues if required (before any early-outs).
        if (cascadeToOtherRefreshQueues)
            RefreshQueueAreaAdded?.Invoke(new RefreshQueueAreaAddedEventArgs(_sceneLayer, pixelRange));

        // Fast containment check: if any existing rect already fully contains this one, skip it.
        bool isContained = false;
        for (int i = 0; i < _rects.Count; i++)
        {
            if (_rects[i].Contains(pixelRange))
            {
                isContained = true;
                break;
            }
        }

        if (!isContained)
        {
            // New contributing area: mark dirty and store it.
            _rects.Add(pixelRange);
            _isDirty = true;
        }
    }

    /// <summary>
    /// Clears all queued refresh areas and tile results.
    /// </summary>
    internal void ClearRefreshQueue()
    {
        // Clear per-tile partial refresh trackers
        foreach (Tile tile in _tiles)
            tile.DrawLocationRefresh?.Clear();

        _tiles.Clear();
        _tileSet.Clear();
        _rects.Clear();
        _isDirty = false;
    }

    /// <summary>
    /// Resolve queued pixel ranges to the minimal set of tiles and sprites to redraw.
    /// </summary>
    private void FindTilesInRange()
    {
        // We’ll collect tiles touched per-area, compute partial refresh per hit tile, and dedupe via _tileSet.
        for (int r = 0; r < _rects.Count; r++)
        {
            Rectangle area = _rects[r];

            // 1) Grid tiles within area
            foreach (SceneLayerTile gridPt in _sceneLayer.CoordinateSystem.GetSceneLayerTilesInPixelRange(_sceneLayer, area, true))
            {
                if (gridPt is null) continue;

                if (_tileSet.Add(gridPt))
                    _tiles.Add(gridPt);

                // Add partial refresh for this tile only if the area intersects it.
                Rectangle tileRefresh = Rectangle.Intersect(area, gridPt.DrawLocation);
                if (!tileRefresh.IsEmpty && gridPt.DrawLocationRefresh is not null && !gridPt.DrawLocationRefresh.Contains(tileRefresh))
                    gridPt.DrawLocationRefresh.Add(tileRefresh);
            }

            // 2) Sprites within area
            foreach (Sprite sprite in SpriteManager.GetSpritesInRange(area, _sceneLayer))
            {
                if (sprite.SceneLayer != _sceneLayer || !sprite.Visible)
                    continue;

                if (!sprite.DrawLocation.IntersectsWith(area))
                    continue;

                if (_tileSet.Add(sprite))
                    _tiles.Add(sprite);

                Rectangle tileRefresh = Rectangle.Intersect(area, sprite.DrawLocation);
                if (!tileRefresh.IsEmpty && sprite.DrawLocationRefresh is not null && !sprite.DrawLocationRefresh.Contains(tileRefresh))
                    sprite.DrawLocationRefresh.Add(tileRefresh);
            }
        }

        _isDirty = false;

        // Stable painter’s order if Tile implements IComparable; otherwise no-op.
        _tiles.Sort();
    }

    public void Dispose()
    {
        RefreshQueueAreaAdded = null;
        GC.SuppressFinalize(this);
    }
}
