using Gondwana.Collisions;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Collisions;
using Gondwana.Movement;
using Gondwana.Scenes;
using Newtonsoft.Json;
using System.Drawing;
using System.Numerics;
using System.Runtime.Serialization;

namespace Gondwana.Drawing.Sprites;

/// <summary>
/// Represents a movable visual element on a scene layer that can be positioned, animated, and rendered with collision detection.
/// Sprites support alignment, positioning, animation, and can be managed through the <see cref="SpriteManager"/>.
/// </summary>
[JsonObject(IsReference = true)]
public partial class Sprite : Tile, IMovableOnSceneLayer, ICollisionMovableEntity, IDisposable
{
    /// <summary>
    /// Occurs when the sprite has moved to a new position on the scene layer.
    /// </summary>
    public event Action<SpriteMovedEventArgs>? SpriteMoved;

    /// <summary>
    /// Occurs when the sprite is being disposed.
    /// </summary>
    public event Action<Sprite>? Disposing;

    #region private / internal fields

    [JsonProperty("SceneLayer")]
    internal SceneLayer _sceneLayer;

    private HorizontalAlignment _horizAlign;
    private VerticalAlignment _vertAlign;
    private int _nudgeX;
    private int _nudgeY;
    private Size _renderSize;

    internal bool _pendingDispose = false;

    [JsonProperty("SceneLayerCoordinates")]
    private PointF _sceneLayerCoordinates;

    #endregion private / internal fields

    #region constructors / finalizer

    [JsonConstructor]
    private Sprite() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sprite"/> class with the specified scene layer and frame.
    /// </summary>
    /// <param name="sceneLayer">The scene layer to attach the sprite to.</param>
    /// <param name="frame">The initial frame for the sprite.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sceneLayer"/> is null.</exception>
    protected internal Sprite(SceneLayer sceneLayer, Frame frame)
    {
        if (sceneLayer == null)
            throw new ArgumentNullException(nameof(sceneLayer), "Sprite must be attached to a SceneLayer.");

        _sceneLayer = sceneLayer;
        animator = new Animator(this);
        pauseAnimation = false;
        _horizAlign = HorizontalAlignment.Center;
        _vertAlign = VerticalAlignment.Bottom;
        _nudgeX = 0;
        _nudgeY = 0;
        CurrentFrame = frame;

        if (SpriteManager.Instance.SizeNewSpritesToSceneLayer)
            _renderSize = new Size(_sceneLayer.TileWidth, _sceneLayer.TileHeight);
        else
            _renderSize = CurrentFrame.Tilesheet.TileSize;

        zOrder = 1;

        Movement = new MovementController(this, MovementState.ForSceneLayer(), this.SceneLayer);
        _collider = new TileCollider(this, collisionGroup: CollisionMasks.All, collidesWith: CollisionMasks.All);
        _sceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);

        SpriteManager.Instance.AddSprite(this);
    }

    /// <summary>
    /// Private constructor used when calling the Clone() method on a Sprite.
    /// </summary>
    internal Sprite(Sprite sprite)
    {
        animator = new Animator(this);
        SpriteManager.Instance.AddSprite(this);

        _sceneLayer = sprite._sceneLayer;
        frame = sprite.frame;
        _horizAlign = sprite._horizAlign;
        _vertAlign = sprite._vertAlign;
        _nudgeX = sprite._nudgeX;
        _nudgeY = sprite._nudgeY;
        _renderSize = sprite._renderSize;
        ZOrder = sprite.zOrder;
        visible = sprite.visible;
        _sceneLayerCoordinates = sprite.SceneLayerCoordinates;

        Movement = new MovementController(this, MovementState.ForSceneLayer(), this.SceneLayer);
        _collider = new TileCollider(this, collisionGroup: CollisionMasks.All, collidesWith: CollisionMasks.All);
        _sceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="Sprite"/> class and releases unmanaged resources.
    /// </summary>
    ~Sprite()
    {
        Dispose();
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        animator = new Animator(this);
        pauseAnimation = false;

        Movement = new MovementController(this, MovementState.ForSceneLayer(), this.SceneLayer);
        _collider = new TileCollider(this, collisionGroup: CollisionMasks.All, collidesWith: CollisionMasks.All);

        if (_sceneLayer != null)
        {
            _sceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
        }

        SpriteManager.Instance.AddSprite(this);
    }

    #endregion constructors / finalizer

    #region IMovable / ICollisionMovableEntity Members

    /// <summary>
    /// Gets the movement space used by this sprite.
    /// </summary>
    public MovementSpace PositionSpace => MovementSpace.Grid;

    /// <summary>
    /// Gets the current position of the sprite in scene layer coordinates.
    /// </summary>
    /// <returns>A <see cref="Vector2"/> representing the sprite's position.</returns>
    public Vector2 GetPosition() => new Vector2(_sceneLayerCoordinates.X, _sceneLayerCoordinates.Y);

    /// <summary>
    /// Sets the position of the sprite in scene layer grid coordinates and updates the display.
    /// </summary>
    /// <param name="pos">The new position for the sprite.</param>
    public void SetPosition(Vector2 pos)
    {
        PointF oldCoord = _sceneLayerCoordinates;
        Rectangle oldDraw = DrawLocationWorld;

        // commit the move
        _sceneLayerCoordinates = new PointF(pos.X, pos.Y);

        // compute destination draw rect AFTER updating coords
        Rectangle newDraw = DrawLocationWorld;

        Rectangle movementWorldRect = Rectangle.Union(oldDraw, newDraw);
        movementWorldRect.Inflate(5, 5);

        _sceneLayer.RefreshQueue.AddWorldRect(movementWorldRect);

        SpriteMoved?.Invoke(new SpriteMovedEventArgs(this, oldCoord, _sceneLayerCoordinates));
    }

    /// <summary>
    /// Applies a world-pixel translation. Used by collision resolution.
    /// </summary>
    /// <param name="dx">The horizontal pixel offset to apply.</param>
    /// <param name="dy">The vertical pixel offset to apply.</param>
    public void TranslateWorldPx(int dx, int dy)
    {
        if (dx == 0 && dy == 0)
            return;

        // Start from current world rect
        var rect = CollisionArea;
        rect.X += dx;
        rect.Y += dy;

        // Convert back to whatever coordinate system Sprite uses internally
        var sceneCoord = GetSceneLayerCoordsFromSpriteWorldRect(rect);

        SetPosition(new Vector2(sceneCoord.X, sceneCoord.Y));
    }

    #endregion IMovable / ICollisionMovableEntity Members

    #region public properties

    /// <summary>
    /// Gets the movement controller for this sprite.
    /// </summary>
    [JsonIgnore]
    public MovementController Movement { get; private set; }

    /// <summary>
    /// Gets or sets the horizontal alignment of the sprite relative to its scene layer coordinates.
    /// </summary>
    [JsonProperty]
    public HorizontalAlignment HorizAlign
    {
        get { return _horizAlign; }
        set
        {
            // add to refresh queue before and after property change
            if (_sceneLayer != null)
            {
                var oldRect = this.DrawLocationWorld;
                _horizAlign = value;
                var newRect = this.DrawLocationWorld;
                _sceneLayer.RefreshQueue.AddWorldRect(Rectangle.Union(oldRect, newRect));
            }
            else
                _horizAlign = value;
        }
    }

    /// <summary>
    /// Gets or sets the vertical alignment of the sprite relative to its scene layer coordinates.
    /// </summary>
    [JsonProperty]
    public VerticalAlignment VertAlign
    {
        get { return _vertAlign; }
        set
        {
            // add to refresh queue before and after property change
            if (_sceneLayer != null)
            {
                var oldRect = this.DrawLocationWorld;
                _vertAlign = value;
                var newRect = this.DrawLocationWorld;
                _sceneLayer.RefreshQueue.AddWorldRect(Rectangle.Union(oldRect, newRect));
            }
            else
                _vertAlign = value;
        }
    }

    /// <summary>
    /// Gets or sets the horizontal pixel offset (nudge) to apply to the sprite's position.
    /// </summary>
    [JsonProperty]
    public int NudgeX
    {
        get { return _nudgeX; }
        set
        {
            // add to refresh queue before and after property change
            if (_sceneLayer != null)
            {
                var oldRect = this.DrawLocationWorld;
                _nudgeX = value;
                var newRect = this.DrawLocationWorld;
                _sceneLayer.RefreshQueue.AddWorldRect(Rectangle.Union(oldRect, newRect));
            }
            else
                _nudgeX = value;
        }
    }

    /// <summary>
    /// Gets or sets the vertical pixel offset (nudge) to apply to the sprite's position.
    /// </summary>
    [JsonProperty]
    public int NudgeY
    {
        get { return _nudgeY; }
        set
        {
            // add to refresh queue before and after property change
            if (_sceneLayer != null)
            {
                var oldRect = this.DrawLocationWorld;
                _nudgeY = value;
                var newRect = this.DrawLocationWorld;
                _sceneLayer.RefreshQueue.AddWorldRect(Rectangle.Union(oldRect, newRect));
            }
            else
                _nudgeY = value;
        }
    }

    /// <summary>
    /// Gets or sets the size at which the sprite will be rendered.
    /// </summary>
    [JsonProperty]
    public Size RenderSize
    {
        get { return _renderSize; }
        set
        {
            // add to refresh queue before and after property change
            if (_sceneLayer != null)
            {
                var oldRect = this.DrawLocationWorld;
                _renderSize = value;
                var newRect = this.DrawLocationWorld;
                
                var unionRect = Rectangle.Union(oldRect, newRect);
                unionRect.Inflate(3, 3);
                _sceneLayer.RefreshQueue.AddWorldRect(unionRect);
            }
            else
                _renderSize = value;
        }
    }

    /// <summary>
    /// Gets the world-space pixel rectangle where the sprite will be drawn, taking into account alignment, size, and nudge offsets.
    /// </summary>
    [JsonIgnore]
    public override Rectangle DrawLocationWorld
    {
        get
        {
            // get the "top left" of the Sprite gridCoordinates value
            Point pxlPt = _sceneLayer.CoordinateSystem.GetAnchorPixelAtSceneLayerCoordinates(_sceneLayer, SceneLayerCoordinates);

            // adjust X coord
            switch (HorizAlign)
            {
                case HorizontalAlignment.Left:
                    // no adjustment necessary
                    break;

                case HorizontalAlignment.Center:
                    // shift right by half the difference between Tile Width values
                    // if Sprite Width > GridPt Width, Sprite will shift left
                    pxlPt.X += (_sceneLayer.TileWidth - _renderSize.Width) / 2;
                    break;

                case HorizontalAlignment.Right:
                    // shift right by the entire difference between Tile Width values
                    // if Sprite Width > GridPt Width, Sprite will shift left
                    pxlPt.X += (_sceneLayer.TileWidth - _renderSize.Width);
                    break;

                default:
                    // shouldn't get here...
                    break;
            }

            // adjust Y coord
            switch (VertAlign)
            {
                case VerticalAlignment.Top:
                    // no adjustment necessary
                    break;

                case VerticalAlignment.Middle:
                    // shift down by half the difference between Tile Height values
                    // if Sprite Height > GridPt Height, Sprite will shift up
                    pxlPt.Y += (_sceneLayer.TileHeight - _renderSize.Height) / 2;
                    break;

                case VerticalAlignment.Bottom:
                    // shift down by the entire difference between Tile Height values
                    // if Sprite Height > GridPt Height, Sprite will shift up
                    pxlPt.Y += (_sceneLayer.TileHeight - _renderSize.Height);
                    break;

                default:
                    // shouldn't get here...
                    break;
            }

            pxlPt.X += NudgeX;
            pxlPt.Y += NudgeY;

            return new Rectangle(pxlPt, _renderSize);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the sprite's position is fixed. Always returns false for sprites.
    /// </summary>
    [JsonIgnore]
    public override bool IsPositionFixed => false;

    /// <summary>
    /// Gets the scene layer coordinates of the sprite.
    /// </summary>
    [JsonIgnore]
    public override PointF SceneLayerCoordinates => _sceneLayerCoordinates;

    /// <summary>
    /// Gets the scene layer that this sprite is attached to.
    /// </summary>
    [JsonIgnore]
    public override SceneLayer SceneLayer => _sceneLayer;

    /// <summary>
    /// Gets or sets the Z-order (depth) of the sprite for rendering. Higher values are drawn on top.
    /// Minimum value is 1.
    /// </summary>
    [JsonProperty]
    public virtual new int ZOrder
    {
        get { return zOrder; }
        set
        {
            if (value < 1)
                base.ZOrder = 1;
            else
                base.ZOrder = value;
        }
    }

    #endregion public properties

    #region internal methods

    /// <summary>
    /// Converts a sprite’s world-pixel rectangle back into SceneLayer coordinates,
    /// reversing any visual placement adjustments (nudges and alignment) applied
    /// during rendering. This is primarily used when collision resolution modifies
    /// a sprite’s position in pixel space and the sprite’s SceneLayer position
    /// must be updated to match.
    /// </summary>
    /// <param name="worldRectPx">The world-pixel rectangle to convert.</param>
    /// <returns>The scene layer coordinates corresponding to the world-pixel rectangle.</returns>
    internal PointF GetSceneLayerCoordsFromSpriteWorldRect(Rectangle worldRectPx)
    {
        // work the Sprites.DrawLocation method backwards...
        worldRectPx.X -= NudgeX;
        worldRectPx.Y -= NudgeY;

        // adjust X coord
        switch (HorizAlign)
        {
            case HorizontalAlignment.Left:
                // no adjustment necessary
                break;

            case HorizontalAlignment.Center:
                // shift left by half the difference between Tile Width values
                // if Sprite Width > GridPt Width, Sprite will shift right
                worldRectPx.X -= (_sceneLayer.TileWidth - worldRectPx.Width) / 2;
                break;

            case HorizontalAlignment.Right:
                // shift left by the entire difference between Tile Width values
                // if Sprite Width > GridPt Width, Sprite will shift right
                worldRectPx.X -= (_sceneLayer.TileWidth - worldRectPx.Width);
                break;

            default:
                // shouldn't get here...
                break;
        }

        // adjust Y coord
        switch (VertAlign)
        {
            case VerticalAlignment.Top:
                // no adjustment necessary
                break;

            case VerticalAlignment.Middle:
                // shift up by half the difference between Tile Height values
                // if Sprite Height > GridPt Height, Sprite will shift down
                worldRectPx.Y -= (_sceneLayer.TileHeight - worldRectPx.Height) / 2;
                break;

            case VerticalAlignment.Bottom:
                // shift up by the entire difference between Tile Height values
                // if Sprite Height > GridPt Height, Sprite will shift down
                worldRectPx.Y -= (_sceneLayer.TileHeight - worldRectPx.Height);
                break;

            default:
                // shouldn't get here...
                break;
        }

        // find and return the grid coordinates after the Sprite adjustments have been considered
        return _sceneLayer.CoordinateSystem.GetSceneLayerCoordinatesAtPixel(_sceneLayer, worldRectPx.Location);
    }

    #endregion

    #region IDisposable Members

    /// <summary>
    /// Releases all resources used by the sprite and removes it from the sprite manager.
    /// </summary>
    public override void Dispose()
    {
        _pendingDispose = true;
    }

    internal void DisposeImmediate()
    {
        GC.SuppressFinalize(this);

        Disposing?.Invoke(this);

        if (_sceneLayer != null)
        {
            // Mark the last draw region as dirty so the background under this sprite is repainted.
            // DrawLocation should already be a world-space rectangle.
            _sceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
        }

        // clear the events
        SpriteMoved = null;
        Disposing = null;

        base.Dispose();
    }

    #endregion IDisposable Members
}