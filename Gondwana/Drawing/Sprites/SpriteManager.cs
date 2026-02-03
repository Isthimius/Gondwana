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

    public static Sprite CreateSprite(SceneLayer sceneLayer, Frame frame)
    {
        Sprite sprite = new Sprite(sceneLayer, frame);
        return sprite;
    }

    public static Sprite CreateSprite(SceneLayer sceneLayer, Frame frame, string id)
    {
        Sprite sprite = CreateSprite(sceneLayer, frame);
        sprite.Nickname = id;

        return sprite;
    }

    public static Sprite CloneSprite(Sprite sprite, SceneLayer sceneLayer)
    {
        Sprite newSprite = new Sprite(sprite);

        if (newSprite.SceneLayer != sceneLayer)
        {
            newSprite._sceneLayer = sceneLayer;
            newSprite._sceneLayer.RefreshQueue.AddWorldRect(newSprite.DrawLocationWorld);
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
            if (sprite.Nickname == ID)
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
                if (range.Contains(sprite.DrawLocationWorld))
                    retSprites.Add(sprite);
            }
            else
            {
                if (sprite.DrawLocationWorld.IntersectsWith(range))
                    retSprites.Add(sprite);
            }
        }

        return retSprites;
    }

    public static List<Sprite> GetSpritesInRange(Rectangle worldRect, SceneLayer sceneLayer, bool fullEnclosures = false)
    {
        List<Sprite> retSprites = new List<Sprite>();

        foreach (Sprite sprite in _spriteList)
        {
            if (sprite.SceneLayer == sceneLayer)
            {
                // check if sprite in range
                if (fullEnclosures)
                {
                    if (worldRect.Contains(sprite.DrawLocationWorld))
                        retSprites.Add(sprite);
                }
                else
                {
                    if (sprite.DrawLocationWorld.IntersectsWith(worldRect))
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
            if (sprite.DrawLocationWorld.Contains(pxlPt))
                retSprites.Add(sprite);
        }

        return retSprites;
    }

    public static List<Sprite> GetSpritesAtPixel(Point pxlPt, SceneLayer sceneLayer)
    {
        List<Sprite> retSprites = new List<Sprite>();

        foreach (Sprite sprite in _spriteList)
        {
            // check if sprite at Point
            if ((sprite.SceneLayer == sceneLayer) && (sprite.DrawLocationWorld.Contains(pxlPt)))
                retSprites.Add(sprite);
        }

        return retSprites;
    }

    #endregion public methods

    #region internal methods

    internal static void MoveSprites(long tick)
    {
        if (tick <= _lastTick)
            return;

        float duration = HighResTimer.GetDuration(_lastTick, tick);

        foreach (var sprite in _spriteList)
        {
            sprite.Movement.AdvanceMovement(duration);
            sprite.AdvanceResize(duration);
        }

        _lastTick = tick;
    }

    #endregion internal methods
}