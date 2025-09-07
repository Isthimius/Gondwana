using Gondwana.Drawing.Animation;
using Gondwana.Scenes;
using System.Collections.ObjectModel;
using System.Drawing;

namespace Gondwana.Drawing.Sprites;

public static class SpriteManager
{
    internal static List<Sprite> _spriteList = new List<Sprite>();

    static SpriteManager()
    {
    }

    public static ReadOnlyCollection<Sprite> AllSprites
    {
        get { return _spriteList.AsReadOnly(); }
    }

    private static bool _sizeNewSpriteToParentGrid = true;
    public static bool SizeNewSpritesToParentGrid
    {
        get { return _sizeNewSpriteToParentGrid; }
        set { _sizeNewSpriteToParentGrid = value; }
    }

    #region public methods
    public static Sprite CreateSprite(SceneLayer matrix, Frame frame)
    {
        Sprite sprite = new Sprite(matrix, frame);
        return sprite;
    }

    public static Sprite CreateSprite(SceneLayer matrix, Frame frame, string ID)
    {
        Sprite sprite = CreateSprite(matrix, frame);
        if (sprite != null)
            sprite.ID = ID;

        return sprite;
    }

    public static Sprite CloneSprite(Sprite sprite, SceneLayer destMatrix)
    {
        Sprite newSprite = (Sprite)sprite.Clone();
        if (newSprite.ParentGrid != destMatrix)
        {
            newSprite.MoveSprite(destMatrix);
            newSprite.ParentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(newSprite.DrawLocation, true);
        }

        return newSprite;
    }

    public static Sprite CloneSprite(string ID, SceneLayer destMatrix)
    {
        Sprite sprite = GetSpriteByID(ID);
        if (sprite != null)
            return CloneSprite(sprite, destMatrix);

        return null;
    }

    public static void Remove(Sprite sprite)
    {
        // Dispose method of Sprite adds area to Ref Queue and removes from spriteList
        sprite.Dispose();
    }

    public static void Remove(string ID)
    {
        Sprite sprite = GetSpriteByID(ID);
        if (sprite != null)
            Remove(sprite);
    }

    public static void Clear()
    {
        List<Sprite> tempSprites = new List<Sprite>(_spriteList);
        foreach (Sprite sprite in tempSprites)
            Remove(sprite);
    }

    public static Sprite? GetSpriteByID(string ID)
    {
        foreach (Sprite sprite in _spriteList)
        {
            if (sprite.ID == ID)
                return sprite;
        }

        return null;
    }

    public static List<Sprite> GetSpritesInRange(Rectangle range)
    {
        return GetSpritesInRange(range, false);
    }

    public static List<Sprite> GetSpritesInRange(Rectangle range, bool fullEnclosures)
    {
        List<Sprite> retSprites = new List<Sprite>();

        foreach (Sprite sprite in _spriteList)
        {
            // check if sprite in range
            if (fullEnclosures)
            {
                if (range.Contains(sprite.DrawLocation))
                    retSprites.Add(sprite);
            }
            else
            {
                if (sprite.DrawLocation.IntersectsWith(range))
                    retSprites.Add(sprite);
            }
        }

        return retSprites;
    }

    public static List<Sprite> GetSpritesInRange(Rectangle range, SceneLayer grid)
    {
        return GetSpritesInRange(range, grid, false);
    }

    public static List<Sprite> GetSpritesInRange(Rectangle range, SceneLayer grid, bool fullEnclosures)
    {
        List<Sprite> retSprites = new List<Sprite>();

        foreach (Sprite sprite in _spriteList)
        {
            if (sprite.ParentGrid == grid)
            {
                // check if sprite in range
                if (fullEnclosures)
                {
                    if (range.Contains(sprite.DrawLocation))
                        retSprites.Add(sprite);
                }
                else
                {
                    if (sprite.DrawLocation.IntersectsWith(range))
                        retSprites.Add(sprite);
                }
            }
        }

        return retSprites;
    }

    public static List<Sprite> GetSpritesAtPixel(Point pxlPt)
    {
        List<Sprite> retSprites = new List<Sprite>();

        foreach (Sprite sprite in _spriteList)
        {
            // check if sprite at Point
            if (sprite.DrawLocation.Contains(pxlPt))
                retSprites.Add(sprite);
        }

        return retSprites;
    }

    public static List<Sprite> GetSpritesAtPixel(Point pxlPt, SceneLayer grid)
    {
        List<Sprite> retSprites = new List<Sprite>();

        foreach (Sprite sprite in _spriteList)
        {
            // check if sprite at Point
            if ((sprite.ParentGrid == grid) && (sprite.DrawLocation.Contains(pxlPt)))
                retSprites.Add(sprite);
        }

        return retSprites;
    }
    #endregion

    #region internal methods
    // TODO: how is this used differently from Sprite.GridCoordinates?
    internal static PointF GridCoordinates(Sprite sprite, SceneLayer grid, Rectangle drawLocation)
    {
        // if Sprite hasn't been placed on SceneLayer, this is moot
        if (grid == null)
            return new PointF();

        // work the Sprites.DrawLocation method backwards...
        drawLocation.X -= sprite.NudgeX;
        drawLocation.Y -= sprite.NudgeY;

        // adjust X coord
        switch (sprite.HorizAlign)
        {
            case HorizontalAlignment.Left:
                // no adjustment necessary
                break;
            case HorizontalAlignment.Center:
                // shift left by half the difference between Tile Width values
                // if Sprite Width > GridPt Width, Sprite will shift right
                drawLocation.X -= (grid.GridPointWidth - drawLocation.Width) / 2;
                break;
            case HorizontalAlignment.Right:
                // shift left by the entire difference between Tile Width values
                // if Sprite Width > GridPt Width, Sprite will shift right
                drawLocation.X -= (grid.GridPointWidth - drawLocation.Width);
                break;
            default:
                // shouldn't get here...
                break;
        }

        // adjust Y coord
        switch (sprite.VertAlign)
        {
            case VerticalAlignment.Top:
                // no adjustment necessary
                break;
            case VerticalAlignment.Middle:
                // shift up by half the difference between Tile Height values
                // if Sprite Height > GridPt Height, Sprite will shift down
                drawLocation.Y -= (grid.GridPointHeight - drawLocation.Height) / 2;
                break;
            case VerticalAlignment.Bottom:
                // shift up by the entire difference between Tile Height values
                // if Sprite Height > GridPt Height, Sprite will shift down
                drawLocation.Y -= (grid.GridPointHeight - drawLocation.Height);
                break;
            default:
                // shouldn't get here...
                break;
        }

        // find and return the grid coordinates after the Sprite adjustments have been considered
        return grid.CoordinateSystem.GetGridPtAtPxl(grid, drawLocation.Location);
    }

    public static void MoveSprites(long tick)
    {
        // advance MovePoints
        for (int i = 0; i < Tile.TilesMoving.Count; i++)
        {
            Sprite sprite = Tile.TilesMoving[i] as Sprite;
            if (sprite != null)
                sprite.movement?.MoveNext(tick);
        }

        // move by velocity
        foreach (Sprite sprite in _spriteList)
        {
            if ((sprite.movement.VelocityX != 0) || (sprite.movement.VelocityY != 0) ||
                (sprite.movement.AccelerationX != 0) || (sprite.movement.AccelerationY != 0))
                sprite.movement.AdjustPositionByVelocity(tick);

            sprite.movement._lastTick = tick;
        }
    }
    #endregion
}
