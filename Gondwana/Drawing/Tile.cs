using System.Drawing;
using Gondwana.Collision;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Collisions;
using Gondwana.Scenes;
using Newtonsoft.Json;

namespace Gondwana.Drawing;

[JsonObject(IsReference = true)]
public abstract class Tile : IComparable<Tile>, IDisposable
{
    #region static members

    public static List<Tile> TilesAnimating { get; private set; }

    static Tile()
    {
        TilesAnimating = new List<Tile>();
    }

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

    #region public fields

    [JsonProperty]
    public object Tag;

    #endregion public fields

    #region abstract properties

    public abstract bool IsPositionFixed { get; }
    public abstract Rectangle DrawLocation { get; }
    public abstract PointF SceneLayerCoordinates { get; }
    public abstract SceneLayer SceneLayer { get; }

    #endregion abstract properties

    [JsonIgnore]
    public virtual Overhang OverhangPixels => frame.Tilesheet?.OverhangPixels ?? Overhang.None;

    [JsonProperty]
    public virtual int ZOrder
    {
        get { return zOrder; }
        set
        {
            zOrder = value;
            SceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(DrawLocation, true);
        }
    }

    [JsonProperty]
    public virtual bool Visible
    {
        get { return visible; }
        set
        {
            visible = value;
            SceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(DrawLocation, true);
        }
    }

    [JsonProperty]
    public virtual Frame CurrentFrame
    {
        get { return frame; }
        set
        {
            // animation might change Tile size, so only add to refresh queue after
            SceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(DrawLocation, true);
            frame = value;
            SceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(DrawLocation, true);
        }
    }

    [JsonIgnore]
    public virtual Animator TileAnimator => animator!;

    [JsonIgnore]
    public virtual bool PauseAnimation { get; set; }

    [JsonIgnore]
    public virtual ICollider? Collider => _collider;

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
            SceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(DrawLocation, true);
        }
    }

    /// <summary>
    /// Used to determine polygonal area when drawing grid lines or fog.
    /// Override this property in a derived class to define custom areas for these effects.
    /// </summary>
    [JsonIgnore]
    public virtual Point[] OutlinePoints => SceneLayer.CoordinateSystem.GetPolygonPts(this, false);

    [JsonProperty]
    public virtual CollisionDetectionAdjustment AdjustCollisionArea { get; set; } = CollisionDetectionAdjustment.None;

    /// <summary>
    /// if position is fixed, use top of primary (i.e., non-overhanging) area;
    /// otherwise, use bottom of location for comparison
    /// </summary>
    private static float GetTileLocForCompare(Tile tile)
    {
        if (!tile.IsPositionFixed)
            return tile.DrawLocation.Bottom - tile.OverhangPixels.Bottom - 1;
        else
            return tile.DrawLocation.Top + tile.OverhangPixels.Top;
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
        return (thisLoc, zOrder, SceneLayerCoordinates.X)
             .CompareTo((tileLoc, tile.zOrder, tile.SceneLayerCoordinates.X));
    }

    #endregion IComparable<Tile> Members

    #region IDisposable Members

    public virtual void Dispose()
    {
        if (TilesAnimating.IndexOf(this) != -1)
            TilesAnimating.Remove(this);

        // dispose any associate Animator instances
        if (animator != null)
            animator.Dispose();

        _collider = null;
    }

    #endregion IDisposable Members
}