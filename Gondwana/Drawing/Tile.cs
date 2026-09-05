using System.Drawing;
using System.Runtime.Serialization;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Collisions;
using Gondwana.Physics.Collisions;
using Gondwana.Rendering;
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
    private TileCollisionType _collisionType = TileCollisionType.None;
    private bool _collisionTypeByFrame;
    private bool _collisionTypeExplicitlySet;
    private bool _collisionsEnabled;
    private string? _collisionProfileName;
    private RenderContext? _sortKeyRenderContext;
    private TileSortKey _sortKey;

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
    /// collision adjustment and collision type. Later frame changes update those values
    /// only when their corresponding by-frame options are enabled.
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

            if (hasFrame &&
                (_collisionTypeByFrame ||
                 (!_hasAssignedFrame && !_collisionTypeExplicitlySet)))
            {
                SetCollisionTypeFromFrame(value);
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

    /// <summary>
    /// Gets or sets the tile's collision behavior. This is initialized from the
    /// first assigned frame and may subsequently be overridden per tile.
    /// </summary>
    [JsonProperty]
    public virtual TileCollisionType CollisionType
    {
        get => _collisionType;
        set
        {
            _collisionTypeExplicitlySet = true;
            SetCollisionTypeCore(value);
        }
    }

    /// <summary>
    /// Gets or sets whether frame changes replace this tile's collision type with
    /// the newly selected frame's effective collision type.
    /// </summary>
    [JsonProperty]
    public virtual bool CollisionTypeByFrame
    {
        get => _collisionTypeByFrame;
        set
        {
            _collisionTypeByFrame = value;

            if (value && frame.Tilesheet is not null)
                SetCollisionTypeFromFrame(frame);
        }
    }

    /// <summary>
    /// Gets the scene-level collision profile currently associated with this tile.
    /// </summary>
    [JsonProperty]
    public string? CollisionProfileName
    {
        get => _collisionProfileName;
        private set => _collisionProfileName = value;
    }

    [JsonProperty]
    public bool CollisionsEnabled
    {
        get => _collisionsEnabled;
        set
        {
            _collisionTypeExplicitlySet = true;

            if (value)
            {
                var enabledType = _collisionType == TileCollisionType.None
                    ? _collider?.ResponseType == CollisionResponseType.Trigger
                        ? TileCollisionType.Trigger
                        : TileCollisionType.Blocking
                    : _collisionType;

                SetCollisionTypeCore(enabledType);
            }
            else
            {
                SetCollisionTypeCore(TileCollisionType.None);
            }
        }
    }

    [JsonIgnore]
    public TypedValueBag ValueBag { get; } = new();

    /// <summary>
    /// Applies a named profile from this tile's parent scene. If the layer has not
    /// yet been attached to a scene, the name is retained and resolved later when
    /// the collider is attached or the profile is reapplied.
    /// </summary>
public void SetCollisionProfile(string profileName)
{
    if (string.IsNullOrWhiteSpace(profileName))
        throw new ArgumentException("Collision profile name cannot be empty.", nameof(profileName));

    var scene = SceneLayer.Scene;
    if (scene is not null)
    {
        var profile = scene.CollisionProfiles.Get(profileName);
        _ = profile.ResolveCollisionGroup(scene.CollisionGroups);
        _ = profile.ResolveCollidesWith(scene.CollisionGroups);
    }

    CollisionProfileName = profileName;
    ApplyCollisionProfileToCollider();
}

    /// <summary>
    /// Resolves the retained profile name again after the tile's layer becomes
    /// attached to a scene.
    /// </summary>
    internal void RefreshCollisionProfile() => ApplyCollisionProfileToCollider();

    /// <summary>
    /// Copies collision behavior, profile, and effective frame metadata from another tile.
    /// </summary>
    protected void CopyCollisionSettingsFrom(Tile source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _adjustCollisionArea = source._adjustCollisionArea;
        _adjustCollisionAreaByFrame = source._adjustCollisionAreaByFrame;
        _hasAssignedFrame = frame.Tilesheet is not null;
        _collisionAdjustExplicitlySet = source._collisionAdjustExplicitlySet;
        _collisionType = source._collisionType;
        _collisionTypeByFrame = source._collisionTypeByFrame;
        _collisionTypeExplicitlySet = source._collisionTypeExplicitlySet;
        _collisionsEnabled = source._collisionsEnabled;
        _collisionProfileName = source._collisionProfileName;
    }

    private void SetCollisionAdjustFromFrame(Frame value)
    {
        // Deliberately bypass the public setter: following a frame should not mark
        // the adjustment as an explicit tile-level override.
        _adjustCollisionArea = value.CollisionAdjust;
    }

    private void SetCollisionTypeFromFrame(Frame value)
    {
        // Deliberately bypass the public setter: following a frame should not mark
        // the collision type as an explicit tile-level override.
        SetCollisionTypeCore(value.CollisionType);
    }

    private void SetCollisionTypeCore(TileCollisionType collisionType)
    {
        _collisionType = collisionType;
        ApplyCollisionTypeToCollider();
        SetCollisionsEnabledCore(collisionType != TileCollisionType.None);
    }

    private void ApplyCollisionTypeToCollider()
    {
        if (_collider is null)
            return;

        switch (_collisionType)
        {
            case TileCollisionType.Blocking:
                _collider.ResponseType = CollisionResponseType.Solid;
                break;
            case TileCollisionType.Trigger:
                _collider.ResponseType = CollisionResponseType.Trigger;
                break;
        }
    }

    private void SetCollisionsEnabledCore(bool enabled)
    {
        _collisionsEnabled = enabled;
        SynchronizeColliderRegistration();
    }

    private void SynchronizeColliderRegistration()
    {
        if (_collider is null)
            return;

        if (_collisionsEnabled)
            SceneLayer.ColliderRegistry.Register(_collider);
        else
            SceneLayer.ColliderRegistry.Unregister(_collider);
    }

    private void ApplyCollisionProfileToCollider()
    {
        if (_collider is null || string.IsNullOrWhiteSpace(_collisionProfileName))
            return;

        var scene = SceneLayer.Scene;
        if (scene is null)
            return;

        var profile = scene.CollisionProfiles.Get(_collisionProfileName);
        _collider.CollisionGroup = profile.ResolveCollisionGroup(scene.CollisionGroups);
        _collider.CollidesWith = profile.ResolveCollidesWith(scene.CollisionGroups);
    }

    /// <summary>
    /// Attaches this tile's collider and applies any collision state that was set
    /// before the collider became available.
    /// </summary>
    protected void AttachCollider(ICollider collider)
    {
        ArgumentNullException.ThrowIfNull(collider);

        if (_collider is not null)
            SceneLayer.ColliderRegistry.Unregister(_collider);

        _collider = collider;
        ApplyCollisionProfileToCollider();
        ApplyCollisionTypeToCollider();
        SynchronizeColliderRegistration();
    }

    [OnDeserialized]
    private void OnTileDeserialized(StreamingContext context)
    {
        _hasAssignedFrame = frame.Tilesheet is not null;

        if (_adjustCollisionAreaByFrame && _hasAssignedFrame)
            SetCollisionAdjustFromFrame(frame);

        if (_collisionTypeByFrame && _hasAssignedFrame)
            SetCollisionTypeFromFrame(frame);
    }

    #region IComparable<Tile> Members

    public int CompareTo(Tile? tile)
    {
        if (tile is null)
            return -1;

        var thisKey = GetSortKeyForCompare();
        var tileKey = tile.GetSortKeyForCompare();

        if (thisKey.IsPositionFixed && !tileKey.IsPositionFixed)
            return -1;

        if (!thisKey.IsPositionFixed && tileKey.IsPositionFixed)
            return 1;

        return (thisKey.Location, thisKey.ZOrder, thisKey.SceneLayerX)
            .CompareTo((tileKey.Location, tileKey.ZOrder, tileKey.SceneLayerX));
    }

    private TileSortKey GetSortKeyForCompare()
    {
        var renderContext = RenderContext.Current;

        // Outside an active render pass preserve the historical live comparison
        // semantics. During rendering, capture the expensive virtual/property values
        // once per tile and reuse them for every comparison in this sort pass.
        if (renderContext is null)
            return CaptureSortKey();

        if (!ReferenceEquals(_sortKeyRenderContext, renderContext))
        {
            _sortKey = CaptureSortKey();
            _sortKeyRenderContext = renderContext;
        }

        return _sortKey;
    }

    private TileSortKey CaptureSortKey()
    {
        bool isPositionFixed = IsPositionFixed;
        Rectangle drawLocationWorld = DrawLocationWorld;
        Spacing overhang = Overhang;
        float location = isPositionFixed
            ? drawLocationWorld.Top + overhang.Top
            : drawLocationWorld.Bottom - overhang.Bottom - 1;

        return new TileSortKey(
            isPositionFixed,
            location,
            zOrder,
            SceneLayerCoordinates.X);
    }

    private readonly record struct TileSortKey(
        bool IsPositionFixed,
        float Location,
        int ZOrder,
        float SceneLayerX);

    #endregion IComparable<Tile> Members

    #region IDisposable Members

    public virtual void Dispose()
    {
        if (TilesAnimating.IndexOf(this) != -1)
            TilesAnimating.Remove(this);

        animator?.Dispose();

        if (_collider is not null)
            SceneLayer.ColliderRegistry.Unregister(_collider);

        _collider = null;
    }

    #endregion IDisposable Members
}