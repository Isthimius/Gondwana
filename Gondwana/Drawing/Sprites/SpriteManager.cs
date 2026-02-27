using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using Gondwana.Timers;
using System.Collections.ObjectModel;
using System.Drawing;

namespace Gondwana.Drawing.Sprites;

public static class SpriteManager
{
    internal readonly static List<Sprite> _spriteList = new List<Sprite>();

    private static long _lastTick = HighResTimer.GetCurrentTick();

    public static event Action<Sprite>? SpriteCreated;

    static SpriteManager() { }

    public static ReadOnlyCollection<Sprite> AllSprites => _spriteList.AsReadOnly();  
    public static bool SizeNewSpritesToSceneLayer { get; set; } = true;

    #region public methods

    public static Sprite CreateSprite(SceneLayer sceneLayer, Frame frame, string? id = null)
    {
        var sprite = new Sprite(sceneLayer, frame);
        sprite.Nickname = id;
        SpriteCreated?.Invoke(sprite);
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

        SpriteCreated?.Invoke(newSprite);
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

    // world
    public static List<Sprite> GetSpritesInWorldRectRange(Rectangle worldRect, SceneLayer? sceneLayer = null, bool fullEnclosures = false)
    {
        List<Sprite> retSprites = new List<Sprite>();

        foreach (Sprite sprite in _spriteList)
        {
            if ((sceneLayer is null) || (sprite.SceneLayer == sceneLayer))
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

    // screen
    public static List<Sprite> GetSpritesInViewRectRange(
        View view,
        Rectangle viewRectPx,
        SceneLayer? sceneLayer = null,
        bool fullEnclosures = false)
    {
        var retSprites = new List<Sprite>();

        foreach (var sprite in _spriteList)
        {
            if ((sceneLayer is null) || (sprite.SceneLayer == sceneLayer))
            {
                var rectScreen = sprite.GetDrawLocationScreen(view).ToPixelAlignedRect();

                if (fullEnclosures)
                {
                    if (viewRectPx.Contains(rectScreen))
                        retSprites.Add(sprite);
                }
                else
                {
                    if (rectScreen.IntersectsWith(viewRectPx))
                        retSprites.Add(sprite);
                }
            }
        }

        return retSprites;
    }

    // screen
    public static List<Sprite> GetSpritesAtViewPixel(View view, Point viewPxlPt, SceneLayer? sceneLayer = null)
    {
        var retSprites = new List<Sprite>();

        foreach (Sprite sprite in _spriteList)
        {
            if ((sceneLayer is null) || (sprite.SceneLayer == sceneLayer))
            {
                // check if sprite at Point
                if (sprite.GetDrawLocationScreen(view).Contains(viewPxlPt))
                    retSprites.Add(sprite);
            }
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