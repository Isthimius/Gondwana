using Gondwana.Collisions;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Collisions;
using Gondwana.Drawing.Sprites;
using Gondwana.Movement;
using Gondwana.Scenes;
using Newtonsoft.Json;
using System.Drawing;
using System.Numerics;
using System.Runtime.Serialization;

namespace Gondwana.Drawing.Sprites;

[JsonObject(IsReference = true)]
public partial class Sprite : Tile, IMovableOnSceneLayer, IDisposable
{
    public event Action<SpriteMovedEventArgs>? SpriteMoved;

    public event Action<Sprite>? Disposing;

    #region private / internal fields

    [JsonProperty("SceneLayer")]
    internal SceneLayer _sceneLayer;

    private HorizontalAlignment _horizAlign;
    private VerticalAlignment _vertAlign;
    private int _nudgeX;
    private int _nudgeY;
    private Size _renderSize;

    [JsonProperty("SceneLayerCoordinates")]
    private PointF _sceneLayerCoordinates;

    #endregion private / internal fields

    #region constructors / finalizer

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

        if (SpriteManager.SizeNewSpritesToSceneLayer)
            _renderSize = new Size(_sceneLayer.TileWidth, _sceneLayer.TileHeight);
        else
            _renderSize = CurrentFrame.Tilesheet.TileSize;

        zOrder = 1;

        Movement = new MovementController(this, MovementState.ForSceneLayer(), this.SceneLayer);
        _collider = new TileCollider(this, layerMask: 1, collidesWithMask: ~0, isStatic: false);
        _sceneLayer.Scene.CollisionWorld.Register(_collider);
        _sceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);

        SpriteManager._spriteList.Add(this);
    }

    /// <summary>
    /// Private constructor used when calling the Clone() method on a Sprite.
    /// </summary>
    internal Sprite(Sprite sprite)
    {
        animator = new Animator(this);
        SpriteManager._spriteList.Add(this);

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
        _collider = new TileCollider(this, layerMask: 1, collidesWithMask: ~0, isStatic: false);
        _sceneLayer.Scene.CollisionWorld.Register(_collider);
        _sceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
    }

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
        _collider = new TileCollider(this, layerMask: 1, collidesWithMask: ~0, isStatic: false);

        if (_sceneLayer != null)
        {
            _sceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
            _sceneLayer.Scene.CollisionWorld.Register(_collider);
        }

        SpriteManager._spriteList.Add(this);
    }

    #endregion constructors / finalizer

    #region IMovable Members

    public MovementSpace PositionSpace => MovementSpace.Grid;

    public Vector2 GetPosition() => new Vector2(_sceneLayerCoordinates.X, _sceneLayerCoordinates.Y);

    public void SetPosition(Vector2 pos)
    {
        // old and new coordinate space positions
        PointF oldCoord = _sceneLayerCoordinates;
        PointF newCoord = new PointF(pos.X, pos.Y);

        // compute old/new draw rects in WORLD pixels
        Rectangle oldDraw = DrawLocationWorld;
        Rectangle newDraw = DrawLocationWorld;

        // union of old + new = full movement envelope
        Rectangle movementWorldRect = Rectangle.Union(oldDraw, newDraw);
        movementWorldRect.Inflate(new Size(5, 5));

        // commit the move
        _sceneLayerCoordinates = newCoord;

        // enqueue ONE world-space dirty rect for the whole movement
        _sceneLayer.RefreshQueue.AddWorldRect(movementWorldRect);

        // raise the event
        SpriteMoved?.Invoke(new SpriteMovedEventArgs(this, oldCoord, newCoord));
    }

    #endregion IMovable Members

    #region public properties

    [JsonIgnore]
    public MovementController Movement { get; private set; }

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
                _sceneLayer.RefreshQueue.AddWorldRect(Rectangle.Union(oldRect, newRect));
            }
            else
                _renderSize = value;
        }
    }

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

    [JsonIgnore]
    public override bool IsPositionFixed => false;

    [JsonIgnore]
    public override PointF SceneLayerCoordinates => _sceneLayerCoordinates;

    [JsonIgnore]
    public override SceneLayer SceneLayer => _sceneLayer;

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

    public override void Dispose()
    {
        GC.SuppressFinalize(this);

        Disposing?.Invoke(this);

        if (_sceneLayer != null)
        {
            // Mark the last draw region as dirty so the background under this sprite is repainted.
            // DrawLocation should already be a world-space rectangle.
            _sceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);

            if (_collider != null)
            {
                var world = _sceneLayer.Scene?.CollisionWorld;
                if (world != null)
                {
                    world.Unregister(_collider);
                }
            }
        }

        if (SpriteManager._spriteList.IndexOf(this) != -1)
            SpriteManager._spriteList.Remove(this);

        // clear the events
        SpriteMoved = null;
        Disposing = null;

        base.Dispose();
    }

    #endregion IDisposable Members
}