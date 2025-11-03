using System.Collections.ObjectModel;
using System.Drawing;
using Gondwana.Scenes;
using Gondwana.Timers;

namespace Gondwana.Drawing.Sprites;

public static class SpriteManager
{
    internal readonly static List<Sprite> _spriteList = new List<Sprite>();

    private static long _lastTick = HighResTimer.GetCurrentTick();

    static SpriteManager() { }

    public static ReadOnlyCollection<Sprite> AllSprites => _spriteList.AsReadOnly();

    public static bool SizeNewSpritesToSceneLayer { get; set; } = true;

    #region public methods

    public static Sprite CreateSprite(SceneLayer matrix, Frame frame)
    {
        Sprite sprite = new Sprite(matrix, frame);
        return sprite;
    }

    public static Sprite CreateSprite(SceneLayer matrix, Frame frame, string id)
    {
        Sprite sprite = CreateSprite(matrix, frame);
        sprite.ID = id;

        return sprite;
    }

    public static Sprite CloneSprite(Sprite sprite, SceneLayer sceneLayer)
    {
        Sprite newSprite = new Sprite(sprite);

        if (newSprite.SceneLayer != sceneLayer)
        {
            newSprite._sceneLayer = sceneLayer;
            newSprite.SceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(newSprite.DrawLocation, true);
        }

        return newSprite;
    }

    public static Sprite? CloneSprite(string id, SceneLayer sceneLayer)
    {
        Sprite? sprite = GetSpriteByID(id);

        if (sprite != null)
            return CloneSprite(sprite, sceneLayer);

        return null;
    }

    public static void Remove(Sprite sprite)
    {
        // Dispose method of Sprite adds area to Ref Queue and removes from spriteList
        sprite.Dispose();
    }

    public static void Remove(string ID)
    {
        Sprite? sprite = GetSpriteByID(ID);
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

    public static List<Sprite> GetSpritesInRange(Rectangle range, SceneLayer grid, bool fullEnclosures = false)
    {
        List<Sprite> retSprites = new List<Sprite>();

        foreach (Sprite sprite in _spriteList)
        {
            if (sprite.SceneLayer == grid)
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
            if ((sprite.SceneLayer == grid) && (sprite.DrawLocation.Contains(pxlPt)))
                retSprites.Add(sprite);
        }

        return retSprites;
    }

    #endregion public methods

    #region internal methods

    internal static Rectangle GetDrawLocation(Sprite sprite, SceneLayer grid, PointF coord, Size size)
    {
        // if Sprite hasn't been placed on SceneLayer, this is moot
        if (grid == null)
            return new Rectangle();

        // get the "top left" of the Sprite gridCoordinates value
        Point pxlPt = grid.CoordinateSystem.GetAnchorPixelAtSceneLayerCoordinates(grid, coord);

        // adjust X coord
        switch (sprite.HorizAlign)
        {
            case HorizontalAlignment.Left:
                // no adjustment necessary
                break;

            case HorizontalAlignment.Center:
                // shift right by half the difference between Tile Width values
                // if Sprite Width > GridPt Width, Sprite will shift left
                pxlPt.X += (grid.SceneLayerTileWidth - size.Width) / 2;
                break;

            case HorizontalAlignment.Right:
                // shift right by the entire difference between Tile Width values
                // if Sprite Width > GridPt Width, Sprite will shift left
                pxlPt.X += (grid.SceneLayerTileWidth - size.Width);
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
                // shift down by half the difference between Tile Height values
                // if Sprite Height > GridPt Height, Sprite will shift up
                pxlPt.Y += (grid.SceneLayerTileHeight - size.Height) / 2;
                break;

            case VerticalAlignment.Bottom:
                // shift down by the entire difference between Tile Height values
                // if Sprite Height > GridPt Height, Sprite will shift up
                pxlPt.Y += (grid.SceneLayerTileHeight - size.Height);
                break;

            default:
                // shouldn't get here...
                break;
        }

        pxlPt.X += sprite.NudgeX;
        pxlPt.Y += sprite.NudgeY;

        return new Rectangle(pxlPt, size);
    }

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
                drawLocation.X -= (grid.SceneLayerTileWidth - drawLocation.Width) / 2;
                break;

            case HorizontalAlignment.Right:
                // shift left by the entire difference between Tile Width values
                // if Sprite Width > GridPt Width, Sprite will shift right
                drawLocation.X -= (grid.SceneLayerTileWidth - drawLocation.Width);
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
                drawLocation.Y -= (grid.SceneLayerTileHeight - drawLocation.Height) / 2;
                break;

            case VerticalAlignment.Bottom:
                // shift up by the entire difference between Tile Height values
                // if Sprite Height > GridPt Height, Sprite will shift down
                drawLocation.Y -= (grid.SceneLayerTileHeight - drawLocation.Height);
                break;

            default:
                // shouldn't get here...
                break;
        }

        // find and return the grid coordinates after the Sprite adjustments have been considered
        return grid.CoordinateSystem.GetSceneLayerCoordinatesAtPixel(grid, drawLocation.Location);
    }

    internal static void MoveSprites(long tick)
    {
        if (tick <= _lastTick)
            return;

        float duration = HighResTimer.GetDuration(_lastTick, tick);

        foreach (var sprite in _spriteList)
        {
            sprite.Movement.AdvanceMovement(duration);
        }

        _lastTick = tick;
    }

    #endregion internal methods
}