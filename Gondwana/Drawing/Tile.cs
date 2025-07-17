using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Sprites;
using Gondwana.Grid;
using System.Runtime.Serialization;
using System.Drawing;
using Gondwana.Drawing.Collisions;

namespace Gondwana.Drawing;

[DataContract(IsReference = true)]
[KnownType(typeof(GridPoint))]
[KnownType(typeof(Sprite))]
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
    private Tile parentTile;
    protected internal int zOrder;
    protected internal bool visible;

    protected internal Frame frame;
    protected internal bool enableFog = false;
    protected internal List<Tile> childTiles;
    protected internal Animator animator;
    protected bool pauseAnimation;
    protected CollisionDetectionType collisionDetection = CollisionDetectionType.None;
    protected CollisionDetectionAdjustment adjustCollisionArea = new CollisionDetectionAdjustment();
    #endregion

    #region public fields
    [IgnoreDataMember]
    public object Tag;
    #endregion

    #region abstract properties
    public abstract bool IsPositionFixed { get; }
    public abstract Rectangle DrawLocation { get; }
    public abstract PointF GridCoordinates { get; }
    public abstract GridPointMatrix ParentGrid { get; }
    #endregion

    private List<Rectangle> _drawLocationRefresh = new List<Rectangle>();

    [IgnoreDataMember]
    public virtual List<Rectangle> DrawLocationRefresh
    {
        get { return _drawLocationRefresh; }
        internal set { _drawLocationRefresh = value; }
    }

    [IgnoreDataMember]
    public virtual int OverlappingPixels
    {
        get
        {
            if (frame.Tilesheet == null)
                return 0;

            return (int)(frame.Tilesheet.ExtraTopSpaceToPrimaryRatio * ParentGrid.GridPointHeight);
        }
    }

    [DataMember]
    public virtual int ZOrder
    {
        get { return zOrder; }
        set
        {
            zOrder = value;

            if (ParentGrid != null)
                ParentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(DrawLocation, true);

            if (childTiles != null)
            {
                foreach (Tile tile in childTiles)
                    tile.ZOrder = value;
            }
        }
    }

    [DataMember]
    public virtual bool Visible
    {
        get { return visible; }
        set
        {
            visible = value;

            if (ParentGrid != null)
                ParentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(DrawLocation, true);

            if (childTiles != null)
            {
                foreach (Tile tile in childTiles)
                    tile.Visible = value;
            }
        }
    }

    [DataMember]
    public virtual Frame CurrentFrame
    {
        get { return frame; }
        set
        {
            // animation doesn't change Sprite size, so only add to refresh queue after
            frame = value;

            if (ParentGrid != null)
                ParentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(DrawLocation, true);

            if (childTiles != null)
            {
                foreach (Tile tile in childTiles)
                    tile.CurrentFrame = value;
            }
        }
    }

    [IgnoreDataMember]
    public virtual Animator TileAnimator
    {
        get { return animator; }
    }

    [IgnoreDataMember]
    public virtual bool PauseAnimation
    {
        get { return pauseAnimation; }
        set
        {
            pauseAnimation = value;

            if (childTiles != null)
            {
                foreach (Tile tile in childTiles)
                    tile.PauseAnimation = value;
            }
        }
    }

    [DataMember]
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

            if (childTiles != null)
            {
                foreach (Tile tile in childTiles)
                    tile.DetectCollision = value;
            }
        }
    }

    [IgnoreDataMember]
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

    [DataMember]
    public virtual bool EnableFog
    {
        get { return enableFog; }
        set
        {
            enableFog = value;

            if (ParentGrid != null)
                ParentGrid.RefreshQueue.AddPixelRangeToRefreshQueue(DrawLocation, true);

            if (childTiles != null)
            {
                foreach (Tile tile in childTiles)
                    tile.EnableFog = value;
            }
        }
    }

    [IgnoreDataMember]
    public Tile ParentTile
    {
        get { return parentTile; }
    }

    [IgnoreDataMember]
    public bool IsChildTile
    {
        get { return parentTile != null; }
    }

    /// <summary>
    /// This property is used to determine polygonal area when drawing grid lines or fog.
    /// Override this property in a derived class to define custom areas for these effects.
    /// </summary>
    [IgnoreDataMember]
    public virtual Point[] OutlinePoints
    {
        get { return ParentGrid.CoordinateSystem.GetPolygonPts(this, false); }
    }

    [DataMember]
    public virtual CollisionDetectionAdjustment AdjustCollisionArea
    {
        get { return adjustCollisionArea; }
        set
        {
            adjustCollisionArea = value;

            if (childTiles != null)
            {
                foreach (Tile tile in childTiles)
                    tile.AdjustCollisionArea = value;
            }
        }
    }

    /// <summary>
    /// if position is fixed, use top of primary (i.e., non-overlapping) area;
    /// otherwise, use bottom of location for comparison
    /// </summary>
    /// <param name="tile">the Tile that is being checked to find value for comparison</param>
    /// <returns></returns>
    private float GetTileLocForCompare(Tile tile)
    {
        if (!tile.IsPositionFixed)
            return tile.DrawLocation.Bottom - 1;
        else
            return tile.DrawLocation.Top + tile.OverlappingPixels;
    }

    protected internal void AddChild(Tile child)
    {
        if (childTiles == null)
            childTiles = new List<Tile>();

        if (childTiles.IndexOf(child) == -1)
        {
            child.parentTile = this;
            childTiles.Add(child);
        }
    }

    protected internal void DisposeChildTiles()
    {
        // call Dispose() on all child Tiles
        if (childTiles != null)
        {
            Tile[] tiles = new Tile[childTiles.Count];
            childTiles.CopyTo(tiles);

            foreach (Tile tile in tiles)
                tile.Dispose();
        }
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
        // call Dispose() on all child Tiles
        DisposeChildTiles();

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
