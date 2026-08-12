using System.Drawing;
using System.Numerics;
using System.Runtime.Serialization;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Collisions;
using Gondwana.Physics.Collisions;
using Gondwana.Physics.Movement;
using Gondwana.Rendering.Views;
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
    private float _rotation;

    internal bool _pendingDispose;

    [JsonProperty("SceneLayerCoordinates")]
    private PointF _sceneLayerCoordinates;

    [JsonConstructor]
    private Sprite() { }

    protected internal Sprite(SceneLayer sceneLayer, Frame frame)
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
        CurrentFrame = frame;

        _renderSize = SpriteManager.Instance.SizeNewSpritesToSceneLayer
            ? new Size(_sceneLayer.TileWidth, _sceneLayer.TileHeight)
            : CurrentFrame.TileSize;

        zOrder = 1;

        Movement = new MovementController(
            this,
            MovementState.ForSceneLayer(),
            SceneLayer);

        _collider = new TileCollider(
            this,
            collisionGroup: CollisionMasks.All,
            collidesWith: CollisionMasks.All);

        _sceneLayer.RefreshQueue.AddWorldRect(VisualBoundsWorld);
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
        _rotation = sprite._rotation;
        ZOrder = sprite.zOrder;
        visible = sprite.visible;
        _sceneLayerCoordinates = sprite.SceneLayerCoordinates;

        Movement = new MovementController(
            this,
            MovementState.ForSceneLayer(),
            SceneLayer);

        _collider = new TileCollider(
            this,
            collisionGroup: CollisionMasks.All,
            collidesWith: CollisionMasks.All);

        _sceneLayer.RefreshQueue.AddWorldRect(VisualBoundsWorld);
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

        _collider = new TileCollider(
            this,
            collisionGroup: CollisionMasks.All,
            collidesWith: CollisionMasks.All);

        if (_sceneLayer != null)
            _sceneLayer.RefreshQueue.AddWorldRect(VisualBoundsWorld);

        SpriteManager.Instance.AddSprite(this);
    }

    public MovementSpace PositionSpace => MovementSpace.Grid;

    public Vector2 GetPosition() =>
        new(_sceneLayerCoordinates.X, _sceneLayerCoordinates.Y);

    public void SetPosition(Vector2 pos)
    {
        PointF oldCoord = _sceneLayerCoordinates;
        Rectangle oldDraw = VisualBoundsWorld;

        _sceneLayerCoordinates = new PointF(pos.X, pos.Y);

        Rectangle newDraw = VisualBoundsWorld;
        Rectangle movementWorldRect = Rectangle.Union(oldDraw, newDraw);
        movementWorldRect.Inflate(5, 5);

        _sceneLayer.RefreshQueue.AddWorldRect(movementWorldRect);
        SpriteMoved?.Invoke(
            new SpriteMovedEventArgs(this, oldCoord, _sceneLayerCoordinates));
    }

    /// <summary>
    /// Gets or sets the clockwise visual rotation, in degrees, around the
    /// center of the sprite's render rectangle.
    /// </summary>
    /// <remarks>
    /// Rotation affects rendering and dirty-region bounds. Collision geometry
    /// remains axis-aligned and continues to use <see cref="Tile.CollisionArea"/>.
    /// </remarks>
    [JsonProperty]
    public float Rotation
    {
        get => _rotation;
        set
        {
            if (_rotation.Equals(value))
                return;

            Rectangle oldBounds = VisualBoundsWorld;
            _rotation = value;
            InvalidateVisualChange(oldBounds, VisualBoundsWorld);
        }
    }

    /// <summary>
    /// Gets the axis-aligned world-pixel bounds enclosing the rotated sprite.
    /// </summary>
    [JsonIgnore]
    public Rectangle VisualBoundsWorld =>
        GetRotatedBounds(DrawLocationWorld, Rotation);

    /// <summary>
    /// Gets the axis-aligned screen-pixel bounds enclosing the rotated sprite.
    /// </summary>
    public RectangleF GetVisualBoundsScreen(View view) =>
        GetRotatedBounds(GetDrawLocationScreen(view), Rotation);

    internal RectangleF GetVisualBoundsScreen(RectangleF renderRectScreen) =>
        GetRotatedBounds(renderRectScreen, Rotation);

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
    public override Frame CurrentFrame
    {
        get => base.CurrentFrame;
        set
        {
            Rectangle oldBounds = VisualBoundsWorld;
            base.CurrentFrame = value;
            InvalidateVisualChange(oldBounds, VisualBoundsWorld);
        }
    }

    [JsonProperty]
    public override bool Visible
    {
        get => visible;
        set
        {
            visible = value;
            _sceneLayer.RefreshQueue.AddWorldRect(VisualBoundsWorld);
        }
    }

    [JsonProperty]
    public HorizontalAlignment HorizAlign
    {
        get => _horizAlign;
        set
        {
            if (_sceneLayer != null)
            {
                var oldRect = VisualBoundsWorld;
                _horizAlign = value;
                InvalidateVisualChange(oldRect, VisualBoundsWorld);
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
                var oldRect = VisualBoundsWorld;
                _vertAlign = value;
                InvalidateVisualChange(oldRect, VisualBoundsWorld);
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
                var oldRect = VisualBoundsWorld;
                _nudgeX = value;
                InvalidateVisualChange(oldRect, VisualBoundsWorld);
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
                var oldRect = VisualBoundsWorld;
                _nudgeY = value;
                InvalidateVisualChange(oldRect, VisualBoundsWorld);
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
                var oldRect = VisualBoundsWorld;
                _renderSize = value;
                InvalidateVisualChange(oldRect, VisualBoundsWorld);
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
    public override int ZOrder
    {
        get => zOrder;
        set
        {
            zOrder = Math.Max(1, value);
            _sceneLayer.RefreshQueue.AddWorldRect(VisualBoundsWorld);
        }
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
            _sceneLayer.RefreshQueue.AddWorldRect(VisualBoundsWorld);

        SpriteMoved = null;
        Disposing = null;

        base.Dispose();
    }

    private void InvalidateVisualChange(Rectangle oldBounds, Rectangle newBounds)
    {
        Rectangle dirtyBounds = Rectangle.Union(oldBounds, newBounds);
        dirtyBounds.Inflate(2, 2);
        _sceneLayer.RefreshQueue.AddWorldRect(dirtyBounds);
    }

    private static Rectangle GetRotatedBounds(Rectangle rect, float degrees)
    {
        RectangleF bounds = GetRotatedBounds(
            new RectangleF(rect.X, rect.Y, rect.Width, rect.Height),
            degrees);

        return Rectangle.FromLTRB(
            (int)MathF.Floor(bounds.Left),
            (int)MathF.Floor(bounds.Top),
            (int)MathF.Ceiling(bounds.Right),
            (int)MathF.Ceiling(bounds.Bottom));
    }

    private static RectangleF GetRotatedBounds(RectangleF rect, float degrees)
    {
        if (rect.IsEmpty || degrees % 360f == 0f)
            return rect;

        float radians = degrees * MathF.PI / 180f;
        float sin = MathF.Abs(MathF.Sin(radians));
        float cos = MathF.Abs(MathF.Cos(radians));
        float width = rect.Width * cos + rect.Height * sin;
        float height = rect.Width * sin + rect.Height * cos;
        float centerX = rect.Left + rect.Width * 0.5f;
        float centerY = rect.Top + rect.Height * 0.5f;

        return new RectangleF(
            centerX - width * 0.5f,
            centerY - height * 0.5f,
            width,
            height);
    }
}
