using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using Gondwana.Timers;
using System.Collections.ObjectModel;
using System.Drawing;

namespace Gondwana.Drawing.Sprites;

public sealed class SpriteManager : IDisposable
{
    private static readonly Lazy<SpriteManager> _instance = new(() => new SpriteManager());

    public static SpriteManager Instance => _instance.Value;

    internal readonly List<Sprite> _spriteList = new();

    private long _lastTick = HighResTimer.GetCurrentTick();

    /// <summary>
    /// Event raised when a new sprite is created.
    /// </summary>
    public event Action<Sprite>? SpriteCreated;

    private SpriteManager() { }

    /// <summary>
    /// Gets a read-only collection of all sprites currently managed by the sprite manager.
    /// </summary>
    public ReadOnlyCollection<Sprite> AllSprites => _spriteList.AsReadOnly();

    /// <summary>
    /// Gets or sets a value indicating whether new sprites should be automatically sized to their scene layer.
    /// </summary>
    public bool SizeNewSpritesToSceneLayer { get; set; } = true;

    #region public methods

    /// <summary>
    /// Creates a new sprite on the specified scene layer with the given frame.
    /// </summary>
    /// <param name="sceneLayer">The scene layer on which to create the sprite.</param>
    /// <param name="frame">The frame to use for the sprite.</param>
    /// <param name="id">Optional nickname/identifier for the sprite.</param>
    /// <returns>The newly created sprite.</returns>
    public Sprite CreateSprite(SceneLayer sceneLayer, Frame frame, string? id = null)
    {
        var sprite = new Sprite(sceneLayer, frame);
        sprite.Nickname = id;
        SpriteCreated?.Invoke(sprite);
        return sprite;
    }

    /// <summary>
    /// Creates a copy of the specified sprite, preserving its current scene layer.
    /// </summary>
    /// <param name="sprite">The sprite to clone. Cannot be null.</param>
    /// <returns>A new Sprite instance that is a copy of the specified sprite, assigned to the same scene layer.</returns>
    public Sprite CloneSprite(Sprite sprite) => CloneSprite(sprite, sprite.SceneLayer);

    /// <summary>
    /// Creates a clone of the specified sprite on the given scene layer.
    /// </summary>
    /// <param name="sprite">The sprite to clone.</param>
    /// <param name="sceneLayer">The scene layer for the cloned sprite.</param>
    /// <returns>The cloned sprite.</returns>
    public Sprite CloneSprite(Sprite sprite, SceneLayer sceneLayer)
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

    /// <summary>
    /// Creates a clone of the sprite with the specified ID on the given scene layer.
    /// </summary>
    /// <param name="id">The ID/nickname of the sprite to clone.</param>
    /// <param name="sceneLayer">The scene layer for the cloned sprite.</param>
    /// <returns>The cloned sprite, or null if no sprite with the specified ID exists.</returns>
    public Sprite? CloneSprite(string id, SceneLayer sceneLayer)
    {
        Sprite? sprite = GetSpriteByID(id);

        if (sprite != null)
            return CloneSprite(sprite, sceneLayer);

        return null;
    }

    /// <summary>
    /// Removes and disposes the specified sprite.
    /// </summary>
    /// <param name="sprite">The sprite to remove.</param>
    public void Remove(Sprite sprite)
    {
        sprite.Dispose();
    }

    /// <summary>
    /// Removes and disposes the sprite with the specified ID.
    /// </summary>
    /// <param name="ID">The ID/nickname of the sprite to remove.</param>
    public void Remove(string ID)
    {
        Sprite? sprite = GetSpriteByID(ID);
        if (sprite != null)
            Remove(sprite);
    }

    /// <summary>
    /// Removes and disposes all sprites currently managed by the sprite manager.
    /// </summary>
    public void Clear()
    {
        List<Sprite> tempSprites = new(_spriteList);
        foreach (Sprite sprite in tempSprites)
            Remove(sprite);
    }

    /// <summary>
    /// Retrieves a sprite by its ID/nickname.
    /// </summary>
    /// <param name="ID">The ID/nickname of the sprite to retrieve.</param>
    /// <returns>The sprite with the specified ID, or null if not found.</returns>
    public Sprite? GetSpriteByID(string ID)
    {
        foreach (Sprite sprite in _spriteList)
        {
            if (sprite.Nickname == ID)
                return sprite;
        }

        return null;
    }

    // world
    /// <summary>
    /// Gets all sprites within the specified world rectangle range.
    /// </summary>
    /// <param name="worldRect">The world rectangle to search within.</param>
    /// <param name="sceneLayer">Optional scene layer to filter by. If null, searches all layers.</param>
    /// <param name="fullEnclosures">If true, only returns sprites fully contained within the rectangle. If false, returns sprites that intersect with the rectangle.</param>
    /// <returns>A list of sprites within the specified range.</returns>
    public List<Sprite> GetSpritesInWorldRectRange(
        Rectangle worldRect,
        SceneLayer? sceneLayer = null,
        bool fullEnclosures = false)
    {
        List<Sprite> retSprites = new();

        foreach (Sprite sprite in _spriteList)
        {
            if ((sceneLayer is null) || (sprite.SceneLayer == sceneLayer))
            {
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
    /// <summary>
    /// Gets all sprites within the specified view rectangle range.
    /// </summary>
    /// <param name="view">The view to use for coordinate transformation.</param>
    /// <param name="viewRectPx">The view rectangle in pixels to search within.</param>
    /// <param name="sceneLayer">Optional scene layer to filter by. If null, searches all layers.</param>
    /// <param name="fullEnclosures">If true, only returns sprites fully contained within the rectangle. If false, returns sprites that intersect with the rectangle.</param>
    /// <returns>A list of sprites within the specified view range.</returns>
    public List<Sprite> GetSpritesInViewRectRange(
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

    /// <summary>
    /// Gets all sprites at the specified view pixel coordinate.
    /// </summary>
    /// <param name="view">The view to use for coordinate transformation.</param>
    /// <param name="viewPxlPt">The pixel coordinate in the view to check.</param>
    /// <param name="sceneLayer">Optional scene layer to filter by. If null, searches all layers.</param>
    /// <returns>A list of sprites at the specified pixel location.</returns>
    public List<Sprite> GetSpritesAtViewPixel(
        View view,
        Point viewPxlPt,
        SceneLayer? sceneLayer = null)
    {
        var retSprites = new List<Sprite>();

        foreach (Sprite sprite in _spriteList)
        {
            if ((sceneLayer is null) || (sprite.SceneLayer == sceneLayer))
            {
                if (sprite.GetDrawLocationScreen(view).Contains(viewPxlPt))
                    retSprites.Add(sprite);
            }
        }

        return retSprites;
    }

    #endregion public methods

    #region internal methods

    internal void MoveSprites(long tick)
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

    public void Dispose()
    {
        Clear();
    }
}