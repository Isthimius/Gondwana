using System.Drawing;
using System.Numerics;
using System.Runtime.Serialization;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Collisions;
using Gondwana.Physics.Collisions;
using Gondwana.Physics.Movement;
using Gondwana.Scenes;
using Newtonsoft.Json;

namespace Gondwana.Drawing.Sprites;

/// <summary>
/// Represents a movable visual element on a scene layer.
/// </summary>
[JsonObject(IsReference = true)]
public partial class Sprite : Tile, IMovableOnSceneLayer, ICollisionMovableEntity, IDisposable
{
    public event Action<SpriteMovedEventArgs>? SpriteMoved;
    public event Action<Sprite>? Disposing;

    [JsonProperty("SceneLayer")]
    internal SceneLayer _sceneLayer;

    private HorizontalAlignment _horizAlign;
    private VerticalAlignment _vertAlign;
    private int _nudgeX;
    private int _nudgeY;
    private Size _renderSize;

    internal bool _pendingDispose;

    [JsonProperty("SceneLayerCoordinates")]
    private PointF _sceneLayerCoordinates;

    [JsonConstructor]
    private Sprite() { }

    protected internal Sprite(SceneLayer sceneLayer, Frame frame)
        : this(
            sceneLayer,
            frame,
            SpriteManager.Instance.DefaultCollisionProfile)
    {
    }

    protected internal Sprite(
        SceneLayer sceneLayer,
        Frame frame,
        string collisionProfileName)
    {
        _sceneLayer = sceneLayer
            ?? throw new ArgumentNullException(
                nameof(sceneLayer),
                "Sprite must be attached to a SceneLayer.");

        animator = new Animator(this);
        pauseAnimation = false;
        _horizAlign = HorizontalAlignment.Center;
        _vertAlign = VerticalAlignment.Bottom;
        _nudgeX = 0;
        _nudgeY = 0;

        Movement = new MovementController(
            this,
            MovementState.ForSceneLayer(),
            SceneLayer);

        AttachCollider(new TileCollider(
            this,
            collisionGroup: CollisionMasks.None,
            collidesWith: CollisionMasks.None));

        SetCollisionProfile(collisionProfileName);
        CurrentFrame = frame;

        _renderSize = SpriteManager.Instance.SizeNewSpritesToSceneLayer
            ? new Size(_sceneLayer.TileWidth, _sceneLayer.TileHeight)
            : CurrentFrame.TileSize;

        zOrder = 1;

        _sceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
        SpriteManager.Instance.AddSprite(this);
    }

    /// <summary>
    /// Copy constructor used by <see cref="SpriteManager.CloneSprite(Sprite)"/>.
    /// </summary>
    internal Sprite(Sprite sprite, SceneLayer sceneLayer)
    {
        ArgumentNullException.ThrowIfNull(sprite);

        _sceneLayer = sceneLayer
            ?? throw new ArgumentNullException(
                nameof(sceneLayer),
                "Sprite must be attached to a SceneLayer.");

        animator = new Animator(this);

        frame = sprite.frame;
        CopyCollisionSettingsFrom(sprite);
        _horizAlign = sprite._horizAlign;
        _vertAlign = sprite._vertAlign;
        _nudgeX = sprite._nudgeX;
        _nudgeY = sprite._nudgeY;
        _renderSize = sprite._renderSize;
        ZOrder = sprite.zOrder;
        visible = sprite.visible;
        _sceneLayerCoordinates = sprite.SceneLayerCoordinates;

        Movement = new MovementController(
            this,
            MovementState.ForSceneLayer(),
            SceneLayer);

        AttachCollider(new TileCollider(
            this,
            collisionGroup: CollisionMasks.None,
            collidesWith: CollisionMasks.None));

        if (string.IsNullOrWhiteSpace(CollisionProfileName))
            SetCollisionProfile(SpriteManager.Instance.DefaultCollisionProfile);

        _sceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
        SpriteManager.Instance.AddSprite(this);
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

        Movement = new MovementController(
            this,
            MovementState.ForSceneLayer(),
            SceneLayer);

        AttachCollider(new TileCollider(
            this,
            collisionGroup: CollisionMasks.None,
            collidesWith: CollisionMasks.None));

        if (string.IsNullOrWhiteSpace(CollisionProfileName))
            SetCollisionProfile(SpriteManager.Instance.DefaultCollisionProfile);

        if (_sceneLayer != null)
            _sceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);

        SpriteManager.Instance.AddSprite(this);
    }

    public MovementSpace PositionSpace => MovementSpace.Grid;

    public Vector2 GetPosition() =>
        new(_sceneLayerCoordinates.X, _sceneLayerCoordinates.Y);

    public void SetPosition(Vector2 pos)
    {
        PointF oldCoord = _sceneLayerCoordinates;
        Rectangle oldDraw = DrawLocationWorld;

        _sceneLayerCoordinates = new PointF(pos.X, pos.Y);

        Rectangle newDraw = DrawLocationWorld;
        Rectangle movementWorldRect = Rectangle.Union(oldDraw, newDraw);
        movementWorldRect.Inflate(5, 5);

        _sceneLayer.RefreshQueue.AddWorldRect(movementWorldRect);
        SpriteMoved?.Invoke(
            new SpriteMovedEventArgs(this, oldCoord, _sceneLayerCoordinates));
    }

    /// <summary>
    /// Applies a world-pixel translation without rebuilding the absolute position
    /// from an integer rectangle, preserving fractional coordinates on the other axis.
    /// </summary>
    public void TranslateWorldPx(int dx, int dy)
    {
        if (dx == 0 && dy == 0)
            return;

        var originalRect = CollisionArea;
        var translatedRect = originalRect;
        translatedRect.X += dx;
        translatedRect.Y += dy;

        var originalSceneCoord =
            GetSceneLayerCoordsFromSpriteWorldRect(originalRect);
        var translatedSceneCoord =
            GetSceneLayerCoordsFromSpriteWorldRect(translatedRect);

        var sceneDelta = new Vector2(
            translatedSceneCoord.X - originalSceneCoord.X,
            translatedSceneCoord.Y - originalSceneCoord.Y);

        SetPosition(GetPosition() + sceneDelta);
    }

    public void CancelVelocityComponent(bool cancelX, bool cancelY)
    {
        Movement.ZeroVelocityComponent(cancelX, cancelY);
    }

    [JsonIgnore]
    public MovementController Movement { get; private set; }

    [JsonProperty]
    public HorizontalAlignment HorizAlign
    {
        get => _horizAlign;
        set
        {
            if (_sceneLayer != null)
            {
                var oldRect = DrawLocationWorld;
                _horizAlign = value;
                _sceneLayer.RefreshQueue.AddWorldRect(
                    Rectangle.Union(oldRect, DrawLocationWorld));
            }
            else
            {
                _horizAlign = value;
            }
        }
    }

    [JsonProperty]
    public VerticalAlignment VertAlign
    {
        get => _vertAlign;
        set
        {
            if (_sceneLayer != null)
            {
                var oldRect = DrawLocationWorld;
                _vertAlign = value;
                _sceneLayer.RefreshQueue.AddWorldRect(
                    Rectangle.Union(oldRect, DrawLocationWorld));
            }
            else
            {
                _vertAlign = value;
            }
        }
    }

    [JsonProperty]
    public int NudgeX
    {
        get => _nudgeX;
        set
        {
            if (_sceneLayer != null)
            {
                var oldRect = DrawLocationWorld;
                _nudgeX = value;
                _sceneLayer.RefreshQueue.AddWorldRect(
                    Rectangle.Union(oldRect, DrawLocationWorld));
            }
            else
            {
                _nudgeX = value;
            }
        }
    }

    [JsonProperty]
    public int NudgeY
    {
        get => _nudgeY;
        set
        {
            if (_sceneLayer != null)
            {
                var oldRect = DrawLocationWorld;
                _nudgeY = value;
                _sceneLayer.RefreshQueue.AddWorldRect(
                    Rectangle.Union(oldRect, DrawLocationWorld));
            }
            else
            {
                _nudgeY = value;
            }
        }
    }

    [JsonProperty]
    public Size RenderSize
    {
        get => _renderSize;
        set
        {
            if (_sceneLayer != null)
            {
                var oldRect = DrawLocationWorld;
                _renderSize = value;
                var unionRect = Rectangle.Union(oldRect, DrawLocationWorld);
                unionRect.Inflate(3, 3);
                _sceneLayer.RefreshQueue.AddWorldRect(unionRect);
            }
            else
            {
                _renderSize = value;
            }
        }
    }

    [JsonIgnore]
    public override Rectangle DrawLocationWorld
    {
        get
        {
            Point pxlPt = _sceneLayer.CoordinateSystem
                .GetAnchorPixelAtSceneLayerCoordinates(
                    _sceneLayer,
                    SceneLayerCoordinates);

            switch (HorizAlign)
            {
                case HorizontalAlignment.Center:
                    pxlPt.X += (_sceneLayer.TileWidth - _renderSize.Width) / 2;
                    break;
                case HorizontalAlignment.Right:
                    pxlPt.X += _sceneLayer.TileWidth - _renderSize.Width;
                    break;
            }

            switch (VertAlign)
            {
                case VerticalAlignment.Middle:
                    pxlPt.Y += (_sceneLayer.TileHeight - _renderSize.Height) / 2;
                    break;
                case VerticalAlignment.Bottom:
                    pxlPt.Y += _sceneLayer.TileHeight - _renderSize.Height;
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
        get => zOrder;
        set => base.ZOrder = Math.Max(1, value);
    }

    internal PointF GetSceneLayerCoordsFromSpriteWorldRect(Rectangle worldRectPx)
    {
        worldRectPx.X -= NudgeX;
        worldRectPx.Y -= NudgeY;

        switch (HorizAlign)
        {
            case HorizontalAlignment.Center:
                worldRectPx.X -= (_sceneLayer.TileWidth - worldRectPx.Width) / 2;
                break;
            case HorizontalAlignment.Right:
                worldRectPx.X -= _sceneLayer.TileWidth - worldRectPx.Width;
                break;
        }

        switch (VertAlign)
        {
            case VerticalAlignment.Middle:
                worldRectPx.Y -= (_sceneLayer.TileHeight - worldRectPx.Height) / 2;
                break;
            case VerticalAlignment.Bottom:
                worldRectPx.Y -= _sceneLayer.TileHeight - worldRectPx.Height;
                break;
        }

        return _sceneLayer.CoordinateSystem.GetSceneLayerCoordinatesAtPixel(
            _sceneLayer,
            worldRectPx.Location);
    }

    public override void Dispose()
    {
        _pendingDispose = true;
    }

    internal void DisposeImmediate()
    {
        GC.SuppressFinalize(this);
        Disposing?.Invoke(this);

        if (_sceneLayer != null)
            _sceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);

        SpriteMoved = null;
        Disposing = null;

        base.Dispose();
    }
}
