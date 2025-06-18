using Gondwana.Drawing.Sprites;
using Gondwana.EventArgs;
using Gondwana.Grid;
using Gondwana.Rendering;
using System.Collections.ObjectModel;
using System.Drawing;

namespace Gondwana.Drawing;

public class RefreshQueue : IDisposable
{
    #region private / internal field declarations
    private bool isDirty;               // if true, Tiles need to be found
    private List<Tile> _tiles;          // array of Tile objects to be redrawn
    internal List<Rectangle> _rects;    // array of Rectangle areas being refreshed
    internal GridPointMatrix _layer;    // associated Matrix (parent)
    #endregion

    #region events
    internal event RefreshQueueAreaAddedEventHandler RefreshQueueAreaAdded;
    #endregion

    #region constructors / finalizer
    internal RefreshQueue(GridPointMatrix layer)
    {
        isDirty = false;
        _tiles = new List<Tile>();
        _rects = new List<Rectangle>();
        _layer = layer;
    }

    ~RefreshQueue()
    {
        Dispose();
    }
    #endregion

    #region properties
    public List<Tile> Tiles
    {
        get
        {
            if (isDirty)
                FindTilesInRange();

            return _tiles;
        }
    }
    #endregion

    #region public methods
    public void AddPixelRangeToRefreshQueue(Rectangle pixelRange, bool cascadeToOtherMatrixes)
    {
        // limit refresh range to screen resolution
        pixelRange.Intersect(VisibleSurfaces.MaxSurfaceSize);

        // cascade to other refresh queues if required
        if (cascadeToOtherMatrixes)
        {
            if (RefreshQueueAreaAdded != null)
                RefreshQueueAreaAdded(new RefreshQueueAreaAddedEventArgs(_layer, pixelRange));
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
    #endregion

    #region private methods
    private void FindTilesInRange()
    {
        // find all Tile (GridPoint and Sprite) objects in range
        List<Tile> tempTiles = new List<Tile>();

        foreach (Rectangle area in _rects)
        {
            foreach (GridPoint gridPt in _layer.CoordinateSystem.GetGridPtListInPxlRange(_layer, area, true))
            {
                if (gridPt == null)
                    throw new Exception();

                tempTiles.Add(gridPt);
            }

            // find all Sprite objects in range
            foreach (Sprite sprite in Sprites.Sprites.GetSpritesInRange(area, _layer))
            {
                if ((sprite.ParentGrid == _layer) && sprite.Visible)
                {
                    if (sprite.childTiles != null)
                    {
                        foreach (Sprite child in sprite.childTiles)
                        {
                            if (child.DrawLocation.IntersectsWith(area))
                                tempTiles.Add(child);
                        }
                    }

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
    #endregion

    #region IDisposable Members
    public void Dispose()
    {
        RefreshQueueAreaAdded = null;
        GC.SuppressFinalize(this);
    }
    #endregion
}
