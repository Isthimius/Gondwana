using Gondwana.Drawing;
using Gondwana.Drawing.Sprites;
using Gondwana.Scenes;
using System.Collections.ObjectModel;
using System.Drawing;

namespace Gondwana.Rendering;

public class RefreshQueue : IDisposable
{
    private bool isDirty;               // if true, Tiles need to be found
    private List<Tile> _tiles;          // array of Tile objects to be redrawn
    internal List<Rectangle> _rects;    // array of Rectangle areas being refreshed
    internal SceneLayer _sceneLayer;    // associated SceneLayer (parent)

    internal event EventHandler<RefreshQueueAreaAddedEventArgs> RefreshQueueAreaAdded;

    internal RefreshQueue(SceneLayer layer)
    {
        isDirty = false;
        _tiles = new List<Tile>();
        _rects = new List<Rectangle>();
        _sceneLayer = layer;
    }

    ~RefreshQueue()
    {
        Dispose();
    }

    public List<Tile> Tiles
    {
        get
        {
            if (isDirty)
                FindTilesInRange();

            return _tiles;
        }
    }

    public void AddPixelRangeToRefreshQueue(Rectangle pixelRange, bool cascadeToOtherMatrixes)
    {
        // TODO: track and present MaxSurfaceSize from VisibleSurface
        // limit refresh range to screen resolution
        //pixelRange.Intersect(VisibleSurface._allVisibleSurfaces.MaxSurfaceSize);

        // cascade to other refresh queues if required
        if (cascadeToOtherMatrixes)
        {
            RefreshQueueAreaAdded.Invoke(this, new RefreshQueueAreaAddedEventArgs(_sceneLayer, pixelRange));
        }

        // check all existing pixel ranges for an overlap with the new range
        for (int i = 0; i < _rects.Count; i++)
        {
            // if this pixel range is already included, just return
            if (_rects[i].Contains(pixelRange))
                return;
        }

        // if we make it this far, this includes a new area to refresh
        isDirty = true;
        _rects.Add(pixelRange);
    }

    public void ClearRefreshQueue()
    {
        foreach (Tile tile in _tiles)
            tile.DrawLocationRefresh.Clear();

        _tiles.Clear();
        _rects.Clear();
    }

    public bool AreaIntersectsRefreshArea(Rectangle area)
    {
        foreach (Rectangle rect in _rects)
        {
            if (area.IntersectsWith(rect))
                return true;
        }

        return false;
    }

    public ReadOnlyCollection<Rectangle> GetDirtyRectangles()
    {
        return _rects.AsReadOnly();
    }

    private void FindTilesInRange()
    {
        // find all Tile (GridPoint and Sprite) objects in range
        List<Tile> tempTiles = new List<Tile>();

        foreach (Rectangle area in _rects)
        {
            foreach (SceneLayerPoint gridPt in _sceneLayer.CoordinateSystem.GetGridPtListInPxlRange(_sceneLayer, area, true))
            {
                if (gridPt == null) continue;
                tempTiles.Add(gridPt);
            }

            // find all Sprite objects in range
            foreach (Sprite sprite in Sprites.GetSpritesInRange(area, _sceneLayer))
            {
                if (sprite.ParentGrid == _sceneLayer && sprite.Visible)
                {
                    // add the sprite to the queue if it intersects with the area
                    tempTiles.AddRange(sprite.childTiles?.Where(child => child.DrawLocation.IntersectsWith(area)) ?? Enumerable.Empty<Tile>());

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
                Rectangle tileRefresh = Rectangle.Intersect(area, tile.DrawLocation);

                if (tile.DrawLocationRefresh != null && !tileRefresh.IsEmpty && !tile.DrawLocationRefresh.Contains(tileRefresh))
                    tile.DrawLocationRefresh.Add(tileRefresh);
            }
        }

        isDirty = false;
        _tiles.Sort();
    }

    public void Dispose()
    {
        RefreshQueueAreaAdded = null;
        GC.SuppressFinalize(this);
    }
}
