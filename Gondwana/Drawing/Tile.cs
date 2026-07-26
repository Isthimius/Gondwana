using System.Drawing;
using System.Runtime.Serialization;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Collisions;
using Gondwana.Physics.Collisions;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Newtonsoft.Json;

namespace Gondwana.Drawing;

/// <summary>
/// Represents an abstract base class for drawable tiles in the Gondwana engine.
/// Provides core functionality for rendering, animation, collision detection, and scene layer integration.
/// </summary>
[JsonObject(IsReference = true)]
public abstract class Tile : IDrawable, ICollisionEntity, IComparable<Tile>, IDisposable
{
    #region static members

    /// <summary>
    /// Gets the collection of all tiles that are currently animating in the scene.
    /// </summary>
    public static List<Tile> TilesAnimating { get; } = new();

    #endregion static members

    #region fields

    protected internal int zOrder = 0;
    protected internal bool visible;
    protected internal Frame frame;
    protected internal bool enableFog = false;
    protected internal Animator? animator;
    protected bool pauseAnimation;
    protected ICollider? _collider;

    private CollisionAdjust _adjustCollisionArea = CollisionAdjust.None;
    private bool _adjustCollisionAreaByFrame;
    private bool _hasAssignedFrame;
    private bool _collisionAdjustExplicitlySet;
    private bool _collisionsEnabled;

    #endregion fields

    #region abstract properties

    public abstract bool IsPositionFixed { get; }
    public abstract Rectangle DrawLocationWorld { get; }
    public abstract PointF SceneLayerCoordinates { get; }
    public abstract SceneLayer SceneLayer { get; }

    #endregion abstract properties

    #region IDrawable members

    [JsonProperty]
    public Guid Id { get; private set; } = Guid.NewGuid();

    [JsonProperty]
    public string? Nickname { get; set; }

    [JsonProperty]
    public virtual bool Visible
    {
        get => visible;
        set
        {
            visible = value;
            SceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
        }
    }

    [JsonProperty]
    public virtual int ZOrder
    {
        get => zOrder;
        set
        {
            zOrder = value;
            SceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
        }
    }

    public virtual RectangleF GetDrawLocationScreen(View view) =>
        view.WorldRectToScreenRect(SceneLayer, DrawLocationWorld);

    public virtual RectangleF GetCollisionAreaScreen(View view) =>
        view.WorldRectToScreenRect(SceneLayer, CollisionArea);

    public virtual void Draw(BackbufferBase backbuffer, RectangleF destRectScreen) =>
        backbuffer.DrawTileFrame(this, destRectScreen);

    #endregion IDrawable members

    [JsonIgnore]
    public virtual Spacing Overhang => frame.Overhang;

    /// <summary>
    /// Gets or sets the current frame. The initial frame supplies the tile's default
    /// collision adjustment. Later frame changes update collision bounds only when
    /// <see cref="AdjustCollisionAreaByFrame"/> is enabled.
    /// </summary>
    [JsonProperty]
    public virtual Frame CurrentFrame
    {
        get => frame;
        set
        {
            // Animation may change tile size, so invalidate before and after.
            SceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);

            frame = value;

            bool hasFrame = value.Tilesheet is not null;
            if (hasFrame &&
                (_adjustCollisionAreaByFrame ||
                 (!_hasAssignedFrame && !_collisionAdjustExplicitlySet)))
            {
                SetCollisionAdjustFromFrame(value);
            }

            _hasAssignedFrame = hasFrame;

            SceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
        }
    }

    [JsonIgnore]
    public virtual Animator TileAnimator => animator!;

    [JsonIgnore]
    public virtual bool PauseAnimation { get; set; }

    [JsonIgnore]
    public virtual ICollider? Collider => _collider;

    /// <summary>
    /// Gets the effective collision area in world coordinates.
    /// </summary>
    [JsonIgnore]
    public virtual Rectangle CollisionArea =>
        AdjustCollisionArea.ApplyTo(DrawLocationWorld);

    [JsonProperty]
    public virtual bool EnableFog
    {
        get => enableFog;
        set
        {
            enableFog = value;
            SceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
        }
    }

    [JsonIgnore]
    public virtual Point[] OutlinePointsWorld =>
        SceneLayer.CoordinateSystem.GetPolygonPts(this, false);

    /// <summary>
    /// Gets or sets whether frame changes replace the tile's collision adjustment
    /// with the newly selected frame's adjustment.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="false"/>, which keeps the collision area static
    /// across animation frames after the initial frame supplies its default value.
    /// </remarks>
    [JsonProperty]
    public virtual bool AdjustCollisionAreaByFrame
    {
        get => _adjustCollisionAreaByFrame;
        set
        {
            _adjustCollisionAreaByFrame = value;

            if (value && frame.Tilesheet is not null)
                SetCollisionAdjustFromFrame(frame);
        }
    }

    /// <summary>
    /// Gets or sets the collision adjustment used to derive <see cref="CollisionArea"/>.
    /// </summary>
    [JsonProperty]
    public virtual CollisionAdjust AdjustCollisionArea
    {
        get => _adjustCollisionArea;
        set
        {
            _adjustCollisionArea = value;
            _collisionAdjustExplicitlySet = true;
        }
    }

    [JsonProperty]
    public bool CollisionsEnabled
    {
        get => _collisionsEnabled;
        set
        {
            _collisionsEnabled = value;

            if (_collisionsEnabled)
                SceneLayer.ColliderRegistry.Register(_collider!);
            else
                SceneLayer.ColliderRegistry.Unregister(_collider!);
        }
    }

    [JsonIgnore]
    public TypedValueBag ValueBag { get; } = new();

    /// <summary>
    /// Copies collision behavior and the effective collision adjustment from another tile.
    /// </summary>
    protected void CopyCollisionSettingsFrom(Tile source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _adjustCollisionArea = source._adjustCollisionArea;
        _adjustCollisionAreaByFrame = source._adjustCollisionAreaByFrame;
        _hasAssignedFrame = frame.Tilesheet is not null;
        _collisionAdjustExplicitlySet = source._collisionAdjustExplicitlySet;
    }

    private void SetCollisionAdjustFromFrame(Frame value)
    {
        // Deliberately bypass the public setter: following a frame should not mark
        // the adjustment as an explicit tile-level override.
        _adjustCollisionArea = value.CollisionAdjust;
    }

    [OnDeserialized]
    private void OnTileDeserialized(StreamingContext context)
    {
        _hasAssignedFrame = frame.Tilesheet is not null;

        if (_adjustCollisionAreaByFrame && _hasAssignedFrame)
            SetCollisionAdjustFromFrame(frame);
    }

    #region IComparable<Tile> Members

    public int CompareTo(Tile? tile)
    {
        if (tile is null)
            return -1;

        float thisLoc = GetTileLocForCompare(this);
        float tileLoc = GetTileLocForCompare(tile);

        if (IsPositionFixed && !tile.IsPositionFixed)
            return -1;

        if (!IsPositionFixed && tile.IsPositionFixed)
            return 1;

        return (thisLoc, zOrder, SceneLayerCoordinates.X)
            .CompareTo((tileLoc, tile.zOrder, tile.SceneLayerCoordinates.X));
    }

    private static float GetTileLocForCompare(Tile tile) =>
        tile.IsPositionFixed
            ? tile.DrawLocationWorld.Top + tile.Overhang.Top
            : tile.DrawLocationWorld.Bottom - tile.Overhang.Bottom - 1;

    #endregion IComparable<Tile> Members

    #region IDisposable Members

    public virtual void Dispose()
    {
        if (TilesAnimating.IndexOf(this) != -1)
            TilesAnimating.Remove(this);

        animator?.Dispose();

        SceneLayer.ColliderRegistry.Unregister(_collider!);
        _collider = null;
    }

    #endregion IDisposable Members
}
