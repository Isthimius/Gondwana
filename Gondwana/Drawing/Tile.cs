using System.Drawing;
using System.Runtime.Serialization;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Collisions;
using Gondwana.Drawing.Sprites;
using Gondwana.Scenes;
using Newtonsoft.Json;

namespace Gondwana.Drawing;

[JsonObject(IsReference = true)]
public abstract class Tile : IComparable<Tile>, IDisposable
{
    #region static members
    public static List<Tile> TileCollisions { get; private set; }
    public static List<Tile> TilesAnimating { get; private set; }
    public static List<Tile> TilesMoving { get; private set; }

    static Tile()
    {
        TileCollisions = new List<Tile>();
        TilesAnimating = new List<Tile>();
        TilesMoving = new List<Tile>();
    }
    #endregion

    #region fields
    protected internal int zOrder;
    protected internal bool visible;

    protected internal Frame frame;
    protected internal bool enableFog = false;
    protected internal Animator animator;
    protected bool pauseAnimation;
    protected CollisionDetectionType collisionDetection = CollisionDetectionType.None;
    protected CollisionDetectionAdjustment adjustCollisionArea = new CollisionDetectionAdjustment();
    #endregion

    #region public fields
    [JsonIgnore]
    public object Tag;
    #endregion

    #region abstract properties
    public abstract bool IsPositionFixed { get; }
    public abstract Rectangle DrawLocation { get; }
    public abstract PointF GridCoordinates { get; }
    public abstract SceneLayer ParentGrid { get; }
    #endregion

    private List<Rectangle> _drawLocationRefresh = new List<Rectangle>();

    [JsonIgnore]
    public virtual List<Rectangle> DrawLocationRefresh
    {
        get { return _drawLocationRefresh; }
        internal set { _drawLocationRefresh = value; }
    }

    [JsonIgnore]
    public virtual int OverlappingPixels
    {
        get
        {
            if (frame.Tilesheet == null)
                return 0;

            return (int)(frame.Tilesheet.OverlapTopSpaceToPrimaryRatio * ParentGrid.GridPointHeight);
        }
    }

    [JsonProperty]
    public virtual int ZOrder
    {
        get { return zOrder; }
        set
        {
            zOrder = value;
            ParentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(DrawLocation, true);
        }
    }

    [JsonProperty]
    public virtual bool Visible
    {
        get { return visible; }
        set
        {
            visible = value;
            ParentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(DrawLocation, true);
        }
    }

    [JsonProperty]
    public virtual Frame CurrentFrame
    {
        get { return frame; }
        set
        {
            // animation doesn't change Sprite size, so only add to refresh queue after
            frame = value;
            ParentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(DrawLocation, true);
        }
    }

    [JsonIgnore]
    public virtual Animator TileAnimator
    {
        get { return animator; }
    }

    [JsonIgnore]
    public virtual bool PauseAnimation { get; set; }

    [JsonProperty]
    public virtual CollisionDetectionType DetectCollision
    {
        get { return collisionDetection; }
        set
        {
            if (value == CollisionDetectionType.None)
            {
                // if Tile in the collisions List, remove it
                if (TileCollisions.IndexOf(this) != -1)
                    TileCollisions.Remove(this);
            }
            else
            {
                // if Tile not in the collisions List, add it
                if (TileCollisions.IndexOf(this) == -1)
                    TileCollisions.Add(this);
            }

            collisionDetection = value;
        }
    }

    [JsonIgnore]
    public virtual Rectangle CollisionArea
    {
        get
        {
            Rectangle rect = DrawLocation;
            rect.Y += AdjustCollisionArea.Top;
            rect.X += AdjustCollisionArea.Left;
            rect.Height += AdjustCollisionArea.Bottom - AdjustCollisionArea.Top;
            rect.Width += AdjustCollisionArea.Right - AdjustCollisionArea.Left;
            return rect;
        }
    }

    [JsonProperty]
    public virtual bool EnableFog
    {
        get { return enableFog; }
        set
        {
            enableFog = value;
            ParentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(DrawLocation, true);
        }
    }

    /// <summary>
    /// This property is used to determine polygonal area when drawing grid lines or fog.
    /// Override this property in a derived class to define custom areas for these effects.
    /// </summary>
    [JsonIgnore]
    public virtual Point[] OutlinePoints
    {
        get { return ParentGrid.CoordinateSystem.GetPolygonPts(this, false); }
    }

    [JsonProperty]
    public virtual CollisionDetectionAdjustment AdjustCollisionArea { get; set; }

    /// <summary>
    /// if position is fixed, use top of primary (i.e., non-overlapping) area;
    /// otherwise, use bottom of location for comparison
    /// </summary>
    private float GetTileLocForCompare(Tile tile)
    {
        if (!tile.IsPositionFixed)
            return tile.DrawLocation.Bottom - 1;
        else
            return tile.DrawLocation.Top + tile.OverlappingPixels;
    }

    #region IComparable<Tile> Members
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
        return (thisLoc, zOrder, GridCoordinates.X)
             .CompareTo((tileLoc, tile.zOrder, tile.GridCoordinates.X));
    }
    #endregion

    #region IDisposable Members
    public virtual void Dispose()
    {
        // remove Tile from any Engine-level List<> objects
        if (TileCollisions.IndexOf(this) != -1)
            TileCollisions.Remove(this);

        if (TilesAnimating.IndexOf(this) != -1)
            TilesAnimating.Remove(this);

        if (TilesMoving.IndexOf(this) != -1)
            TilesMoving.Remove(this);

        // dispose any associate Animator instances
        if (animator != null)
            animator.Dispose();
    }
    #endregion
}
