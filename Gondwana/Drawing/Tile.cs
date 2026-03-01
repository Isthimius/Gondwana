using System.Drawing;
using Gondwana.Collisions;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Collisions;
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
    /// Used to track and update animated tiles during the render cycle.
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

    #endregion fields

    #region abstract properties

    /// <summary>
    /// Gets a value indicating whether the tile's position is fixed in screen space (e.g., UI elements)
    /// or moves with the world (e.g., game objects).
    /// </summary>
    public abstract bool IsPositionFixed { get; }

    /// <summary>
    /// Gets the tile's draw location in world coordinates as a rectangle.
    /// This represents the area occupied by the tile in the game world.
    /// </summary>
    public abstract Rectangle DrawLocationWorld { get; }
    
    /// <summary>
    /// Gets the tile's position within its scene layer using the layer's coordinate system.
    /// </summary>
    public abstract PointF SceneLayerCoordinates { get; }
    
    /// <summary>
    /// Gets the scene layer that contains this tile.
    /// </summary>
    public abstract SceneLayer SceneLayer { get; }

    #endregion abstract properties

    #region IDrawable members

    /// <summary>
    /// Gets the unique identifier for this tile instance.
    /// </summary>
    [JsonProperty]
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets an optional friendly name for the tile, useful for debugging and identification.
    /// </summary>
    [JsonProperty]
    public string? Nickname { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tile is visible and should be rendered.
    /// Setting this property triggers a refresh of the tile's screen area.
    /// </summary>
    [JsonProperty]
    public virtual bool Visible
    {
        get { return visible; }
        set
        {
            visible = value;
            SceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
        }
    }

    /// <summary>
    /// Gets or sets the Z-order (depth) of the tile for rendering priority.
    /// Higher values are drawn later (on top of lower values).
    /// Setting this property triggers a refresh of the tile's screen area.
    /// </summary>
    [JsonProperty]
    public virtual int ZOrder
    {
        get { return zOrder; }
        set
        {
            zOrder = value;
            SceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
        }
    }

    /// <summary>
    /// Converts the tile's world location to screen coordinates based on the specified view.
    /// </summary>
    /// <param name="view">The view containing camera and viewport information for the transformation.</param>
    /// <returns>The tile's location in screen space as a rectangle.</returns>
    public virtual RectangleF GetDrawLocationScreen(View view)
    {
        return view.WorldRectToScreenRect(SceneLayer, DrawLocationWorld);
    }

    /// <summary>
    /// Converts the tile's collision area from world coordinates to screen coordinates.
    /// </summary>
    /// <param name="view">The view containing camera and viewport information for the transformation.</param>
    /// <returns>The tile's collision area in screen space as a rectangle.</returns>
    public virtual RectangleF GetCollisionAreaScreen(View view)
    {
        return view.WorldRectToScreenRect(SceneLayer, CollisionArea);
    }

    /// <summary>
    /// Renders the tile to the specified backbuffer at the given screen location.
    /// </summary>
    /// <param name="backbuffer">The backbuffer to render to.</param>
    /// <param name="destRectScreen">The destination rectangle in screen coordinates where the tile should be drawn.</param>
    public virtual void Draw(BackbufferBase backbuffer, RectangleF destRectScreen) => backbuffer.DrawTileFrame(this, destRectScreen);

    #endregion IDrawable members

    /// <summary>
    /// Gets the overhang dimensions (in pixels) that extend beyond the tile's primary area.
    /// This is typically used for tiles with visual elements that exceed their logical boundaries.
    /// </summary>
    [JsonIgnore]
    public virtual Overhang OverhangPixels => frame.Tilesheet?.OverhangPixels ?? Overhang.None;

    /// <summary>
    /// Gets or sets the current frame being displayed for this tile.
    /// Setting this property triggers a refresh of both the old and new tile areas to handle size changes.
    /// </summary>
    [JsonProperty]
    public virtual Frame CurrentFrame
    {
        get { return frame; }
        set
        {
            // animation might change Tile size, so add before and after
            SceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
            frame = value;
            SceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
        }
    }

    /// <summary>
    /// Gets the animator responsible for managing frame transitions and animation sequences for this tile.
    /// </summary>
    [JsonIgnore]
    public virtual Animator TileAnimator => animator!;

    /// <summary>
    /// Gets or sets a value indicating whether the tile's animation is currently paused.
    /// </summary>
    [JsonIgnore]
    public virtual bool PauseAnimation { get; set; }

    /// <summary>
    /// Gets the collider used for collision detection with this tile.
    /// Returns null if the tile has no collision detection.
    /// </summary>
    [JsonIgnore]
    public virtual ICollider? Collider => _collider;

    /// <summary>
    /// Gets the effective collision area of the tile in world coordinates,
    /// incorporating any adjustments specified by <see cref="AdjustCollisionArea"/>.
    /// </summary>
    [JsonIgnore]
    public virtual Rectangle CollisionArea
    {
        get
        {
            Rectangle rect = DrawLocationWorld;
            rect.Y += AdjustCollisionArea.Top;
            rect.X += AdjustCollisionArea.Left;
            rect.Height += AdjustCollisionArea.Bottom - AdjustCollisionArea.Top;
            rect.Width += AdjustCollisionArea.Right - AdjustCollisionArea.Left;
            return rect;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether fog of war rendering is enabled for this tile.
    /// Setting this property triggers a refresh of the tile's screen area.
    /// </summary>
    [JsonProperty]
    public virtual bool EnableFog
    {
        get { return enableFog; }
        set
        {
            enableFog = value;
            SceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
        }
    }

    /// <summary>
    /// Used to determine polygonal area when drawing grid lines or fog.
    /// Override this property in a derived class to define custom areas for these effects.
    /// </summary>
    [JsonIgnore]
    public virtual Point[] OutlinePointsWorld => SceneLayer.CoordinateSystem.GetPolygonPts(this, false);

    /// <summary>
    /// Gets or sets the collision area adjustment values that modify the tile's collision boundaries.
    /// Use this to fine-tune the collision detection area relative to the tile's visual bounds.
    /// </summary>
    [JsonProperty]
    public virtual CollisionDetectionAdjustment AdjustCollisionArea { get; set; } = CollisionDetectionAdjustment.None;

    private bool _collisionsEnabled = false;
 
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

    /// <summary>
    /// Gets the value bag for storing arbitrary typed values associated with this tile.
    /// Useful for attaching custom game-specific data without subclassing.
    /// </summary>
    [JsonProperty]
    public TypedValueBag ValueBag { get; } = new();

    #region IComparable<Tile> Members

    /// <summary>
    /// Compares this tile to another tile for sorting purposes.
    /// Fixed-position tiles are rendered first, followed by tiles sorted by Y-coordinate, Z-order, and X-coordinate.
    /// </summary>
    /// <param name="tile">The tile to compare with this instance.</param>
    /// <returns>
    /// A negative value if this tile should be drawn before the other tile,
    /// zero if they have the same draw order,
    /// or a positive value if this tile should be drawn after the other tile.
    /// </returns>
    public int CompareTo(Tile? tile)
    {
        if (tile is null)
            return -1;

        float thisLoc = GetTileLocForCompare(this);
        float tileLoc = GetTileLocForCompare(tile);

        // Handle fixed position vs non-fixed first
        if (IsPositionFixed && !tile.IsPositionFixed)
            return -1;

        if (!IsPositionFixed && tile.IsPositionFixed)
            return 1;

        // Use tuple comparison for the rest (Y, Z, X)
        return (thisLoc, zOrder, SceneLayerCoordinates.X)
             .CompareTo((tileLoc, tile.zOrder, tile.SceneLayerCoordinates.X));
    }

    /// <summary>
    /// if position is fixed, use top of primary (i.e., non-overhanging) area;
    /// otherwise, use bottom of location for comparison
    /// </summary>
    private static float GetTileLocForCompare(Tile tile)
    {
        return tile.IsPositionFixed
            ? tile.DrawLocationWorld.Top + tile.OverhangPixels.Top
            : tile.DrawLocationWorld.Bottom - tile.OverhangPixels.Bottom - 1;
    }
    #endregion IComparable<Tile> Members

    #region IDisposable Members

    /// <summary>
    /// Releases all resources used by the tile, including removing it from animation tracking,
    /// disposing its animator, and clearing collision references.
    /// </summary>
    public virtual void Dispose()
    {
        if (TilesAnimating.IndexOf(this) != -1)
            TilesAnimating.Remove(this);

        // dispose any associate Animator instances
        if (animator != null)
            animator.Dispose();

        SceneLayer.ColliderRegistry.Unregister(_collider!);
        _collider = null;
    }

    #endregion IDisposable Members
}