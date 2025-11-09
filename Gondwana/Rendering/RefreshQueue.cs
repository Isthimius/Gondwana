using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Sprites;
using Gondwana.Scenes;

namespace Gondwana.Rendering;

/// <summary>
/// Represents a queue for managing refresh operations within a SceneLayer.
/// </summary>
/// <remarks>The <see cref="RefreshQueue"/> tracks areas and tiles that need to be refreshed within a scene layer.
/// It provides functionality to add pixel ranges to the refresh queue, clear the queue, and retrieve the tiles that are
/// affected by the refresh operations. This class also raises events when new areas are added to the queue to communicate
/// the needed refresh range to other <see cref="SceneLayer"/>s in the <see cref="Scene"/>.</remarks>
internal sealed class RefreshQueue : IDisposable
{
    private bool _isDirty;              // if true, Tiles need to be found; internal optimaization
    private List<Tile> _tiles;          // array of Tile objects to be redrawn
    private List<Rectangle> _rects;     // array of Rectangle areas being refreshed
    private SceneLayer _sceneLayer;     // associated SceneLayer (parent)

    internal event Action<RefreshQueueAreaAddedEventArgs>? RefreshQueueAreaAdded;

    internal RefreshQueue(SceneLayer layer)
    {
        _isDirty = false;
        _tiles = new List<Tile>();
        _rects = new List<Rectangle>();
        _sceneLayer = layer;
    }

    ~RefreshQueue()
    {
        Dispose();
    }

    internal List<Tile> Tiles
    {
        get
        {
            if (_isDirty)
                FindTilesInRange();

            return _tiles;
        }
    }

    internal void AddPixelRangeToRefreshQueue(Rectangle pixelRange, bool cascadeToOtherRefreshQueues)
    {
        // cascade to other refresh queues if required
        if (cascadeToOtherRefreshQueues)
            RefreshQueueAreaAdded?.Invoke(new RefreshQueueAreaAddedEventArgs(_sceneLayer, pixelRange));

        // check all existing pixel ranges for an overlap with the new range
        for (int i = 0; i < _rects.Count; i++)
        {
            // if this pixel range is already included, just return
            if (_rects[i].Contains(pixelRange))
                return;
        }

        // if we make it this far, this includes a new area to refresh
        _isDirty = true;
        _rects.Add(pixelRange);
    }

    internal void ClearRefreshQueue()
    {
        foreach (Tile tile in _tiles)
            tile.DrawLocationRefresh.Clear();

        _tiles.Clear();
        _rects.Clear();
    }

    private void FindTilesInRange()
    {
        // find all Tile (GridPoint and Sprite) objects in range
        List<Tile> tempTiles = new List<Tile>();

        foreach (Rectangle area in _rects)
        {
            foreach (SceneLayerTile gridPt in _sceneLayer.CoordinateSystem.GetSceneLayerTilesInPixelRange(_sceneLayer, area, true))
            {
                if (gridPt == null) continue;
                tempTiles.Add(gridPt);
            }

            // find all Sprite objects in range
            foreach (Sprite sprite in SpriteManager.GetSpritesInRange(area, _sceneLayer))
            {
                if (sprite.SceneLayer == _sceneLayer && sprite.Visible)
                {
                    if (sprite.DrawLocation.IntersectsWith(area))
                        tempTiles.Add(sprite);
                }
            }

            // update DrawLocationRefresh for all Tile objects in temp queue,
            // and add to main queue if not already there
            foreach (Tile tile in tempTiles)
            {
                if (_tiles.IndexOf(tile) == -1)
                    _tiles.Add(tile);
            }

            // add the new refresh area to the Tile's refresh area
            foreach (Tile tile in _tiles)
            {
                // find intersection of area and Tile's DrawLocation (i.e., allow for partial Tile refresh)
                Rectangle tileRefresh = Rectangle.Intersect(area, tile.DrawLocation);

                if (tile.DrawLocationRefresh != null && !tileRefresh.IsEmpty && !tile.DrawLocationRefresh.Contains(tileRefresh))
                    tile.DrawLocationRefresh.Add(tileRefresh);
            }
        }

        _isDirty = false;
        _tiles.Sort();
    }

    public void Dispose()
    {
        RefreshQueueAreaAdded = null;
        GC.SuppressFinalize(this);
    }
}