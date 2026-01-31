using System.Drawing;
using System.Numerics;
using System.Runtime.Serialization;
using Gondwana.Collision;
using Gondwana.Drawing.Animation;
using Gondwana.Movement;
using Gondwana.Scenes;
using Newtonsoft.Json;

namespace Gondwana.Drawing.Sprites;

[JsonObject(IsReference = true)]
public partial class Sprite : Tile, IMovableOnSceneLayer, IDisposable
{
    public event Action<SpriteMovedEventArgs>? SpriteMoved;

    public event Action<Sprite>? Disposing;

    #region private / internal fields

    [JsonProperty("SceneLayer")]
    internal SceneLayer _sceneLayer;

    private HorizontalAlignment horizAlign;
    private VerticalAlignment vertAlign;
    private int nudgeX;
    private int nudgeY;
    private Size renderSize;

    [JsonProperty("SceneLayerCoordinates")]
    private PointF sceneLayerCoordinates;

    #endregion private / internal fields

    #region constructors / finalizer

    protected internal Sprite(SceneLayer sceneLayer, Frame frame)
    {
        if (sceneLayer == null)
            throw new ArgumentNullException(nameof(sceneLayer), "Sprite must be attached to a SceneLayer.");

        _sceneLayer = sceneLayer;
        animator = new Animator(this);
        pauseAnimation = false;
        horizAlign = HorizontalAlignment.Center;
        vertAlign = VerticalAlignment.Bottom;
        nudgeX = 0;
        nudgeY = 0;
        CurrentFrame = frame;

        if (SpriteManager.SizeNewSpritesToSceneLayer)
            renderSize = new Size(_sceneLayer.SceneLayerTileWidth, _sceneLayer.SceneLayerTileHeight);
        else
            renderSize = CurrentFrame.Tilesheet.TileSize;

        zOrder = 1;

        Movement = new MovementController(this, MovementState.ForSceneLayer(this.GetPosition()), this.SceneLayer);
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
        horizAlign = sprite.horizAlign;
        vertAlign = sprite.vertAlign;
        nudgeX = sprite.nudgeX;
        nudgeY = sprite.nudgeY;
        renderSize = sprite.renderSize;
        ZOrder = sprite.zOrder;
        visible = sprite.visible;
        sceneLayerCoordinates = sprite.SceneLayerCoordinates;

        Movement = new MovementController(this, MovementState.ForSceneLayer(this.GetPosition()), this.SceneLayer);
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

        Movement = new MovementController(this, MovementState.ForSceneLayer(this.GetPosition()), this.SceneLayer);
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

    public Vector2 GetPosition() => new Vector2(sceneLayerCoordinates.X, sceneLayerCoordinates.Y);

    public void SetPosition(Vector2 pos)
    {
        // old and new coordinate space positions
        PointF oldCoord = sceneLayerCoordinates;
        PointF newCoord = new PointF(pos.X, pos.Y);

        // compute old/new draw rects in WORLD pixels
        Rectangle oldDraw = SpriteManager.GetDrawLocation(this, _sceneLayer, oldCoord, renderSize);
        Rectangle newDraw = SpriteManager.GetDrawLocation(this, _sceneLayer, newCoord, renderSize);

        // union of old + new = full movement envelope
        Rectangle movementWorldRect = Rectangle.Union(oldDraw, newDraw);
        movementWorldRect.Inflate(new Size(5, 5));

        // commit the move
        sceneLayerCoordinates = newCoord;

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
        get { return horizAlign; }
        set
        {
            // add to refresh queue before and after property change
            if (_sceneLayer != null)
            {
                var oldRect = this.DrawLocationWorld;
                horizAlign = value;
                var newRect = this.DrawLocationWorld;
                _sceneLayer.RefreshQueue.AddWorldRect(Rectangle.Union(oldRect, newRect));
            }
            else
                horizAlign = value;
        }
    }

    [JsonProperty]
    public VerticalAlignment VertAlign
    {
        get { return vertAlign; }
        set
        {
            // add to refresh queue before and after property change
            if (_sceneLayer != null)
            {
                var oldRect = this.DrawLocationWorld;
                vertAlign = value;
                var newRect = this.DrawLocationWorld;
                _sceneLayer.RefreshQueue.AddWorldRect(Rectangle.Union(oldRect, newRect));
            }
            else
                vertAlign = value;
        }
    }

    [JsonProperty]
    public int NudgeX
    {
        get { return nudgeX; }
        set
        {
            // add to refresh queue before and after property change
            if (_sceneLayer != null)
            {
                var oldRect = this.DrawLocationWorld;
                nudgeX = value;
                var newRect = this.DrawLocationWorld;
                _sceneLayer.RefreshQueue.AddWorldRect(Rectangle.Union(oldRect, newRect));
            }
            else
                nudgeX = value;
        }
    }

    [JsonProperty]
    public int NudgeY
    {
        get { return nudgeY; }
        set
        {
            // add to refresh queue before and after property change
            if (_sceneLayer != null)
            {
                var oldRect = this.DrawLocationWorld;
                nudgeY = value;
                var newRect = this.DrawLocationWorld;
                _sceneLayer.RefreshQueue.AddWorldRect(Rectangle.Union(oldRect, newRect));
            }
            else
                nudgeY = value;
        }
    }

    [JsonProperty]
    public Size RenderSize
    {
        get { return renderSize; }
        set
        {
            // add to refresh queue before and after property change
            if (_sceneLayer != null)
            {
                var oldRect = this.DrawLocationWorld;
                renderSize = value;
                var newRect = this.DrawLocationWorld;
                _sceneLayer.RefreshQueue.AddWorldRect(Rectangle.Union(oldRect, newRect));
            }
            else
                renderSize = value;
        }
    }

    [JsonIgnore]
    public override Rectangle DrawLocationWorld => SpriteManager.GetDrawLocation(this, _sceneLayer, sceneLayerCoordinates, renderSize);

    [JsonIgnore]
    public override bool IsPositionFixed => false;

    [JsonIgnore]
    public override PointF SceneLayerCoordinates => sceneLayerCoordinates;

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