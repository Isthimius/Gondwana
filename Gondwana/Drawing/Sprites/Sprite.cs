using System.Drawing;
using System.Numerics;
using System.Runtime.Serialization;
using Gondwana.Drawing.Animation;
using Gondwana.Movement;
using Gondwana.Scenes;
using Newtonsoft.Json;

namespace Gondwana.Drawing.Sprites;

[JsonObject(IsReference = true)]
public class Sprite : Tile, IMovableOnSceneLayer, IDisposable
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

        _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);

        Movement = new MovementController(this, MovementState.ForSceneLayer(this.GetPosition()), this.SceneLayer);

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
        DetectCollision = sprite.collisionDetection;
        horizAlign = sprite.horizAlign;
        vertAlign = sprite.vertAlign;
        nudgeX = sprite.nudgeX;
        nudgeY = sprite.nudgeY;
        renderSize = sprite.renderSize;
        ZOrder = sprite.zOrder;
        visible = sprite.visible;
        sceneLayerCoordinates = sprite.SceneLayerCoordinates;
        AdjustCollisionArea = sprite.AdjustCollisionArea;

        Movement = new MovementController(this, MovementState.ForSceneLayer(this.GetPosition()), this.SceneLayer);

        _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
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

        if (_sceneLayer != null)
            _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);

        Movement = new MovementController(this, MovementState.ForSceneLayer(this.GetPosition()), this.SceneLayer);

        SpriteManager._spriteList.Add(this);
    }

    #endregion constructors / finalizer

    #region IMovable Members

    public MovementSpace PositionSpace => MovementSpace.Grid;

    public Vector2 GetPosition() => new Vector2(sceneLayerCoordinates.X, sceneLayerCoordinates.Y);

    public void SetPosition(Vector2 pos)
    {
        // capture the Sprite coordinates before the move
        PointF oldCoord = sceneLayerCoordinates;
        PointF newCoord = new PointF(pos.X, pos.Y);

        // add to refresh queue before move, then move, then add to queue after move
        _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
        sceneLayerCoordinates = newCoord;
        _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);

        // raise the SpriteMoved event
        SpriteMoved?.Invoke(new SpriteMovedEventArgs(this, oldCoord, newCoord));
    }

    #endregion IMovable Members

    #region public properties

    [JsonProperty]
    public string ID { get; set; } = Guid.NewGuid().ToString();

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
                _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
                horizAlign = value;
                _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
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
                _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
                vertAlign = value;
                _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
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
                _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
                nudgeX = value;
                _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
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
                _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
                nudgeY = value;
                _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
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
                _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
                renderSize = value;
                _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);
            }
            else
                renderSize = value;
        }
    }

    [JsonIgnore]
    public override Rectangle DrawLocation => SpriteManager.GetDrawLocation(this, _sceneLayer, sceneLayerCoordinates, renderSize);

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

        base.Dispose();

        if (_sceneLayer != null)
        {
            _sceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(this.DrawLocation, true);

            // just added Sprite and overhanging Tile objects to queue,
            // remove the actual Sprite from the queue since it will
            // no longer be available
            _sceneLayer.RefreshQueue.Tiles.Remove(this);
        }

        if (SpriteManager._spriteList.IndexOf(this) != -1)
            SpriteManager._spriteList.Remove(this);

        // clear the events
        SpriteMoved = null;
        Disposing = null;
    }

    #endregion IDisposable Members
}