using Gondwana.Drawing;
using Gondwana.Drawing.Animation;
using System.Drawing;
using Newtonsoft.Json;

namespace Gondwana.Scenes;

/// <summary>
/// Represents the values stored at a single location on a SceneLayer
/// </summary>
public class SceneLayerPoint : Tile, IDisposable
{
    #region private / internal fields
    [JsonProperty]
    internal SceneLayer parentSceneLayer;

    [JsonProperty]
    internal Point gridCoordinates;         // each SceneLayerPoint knows its location in the array in parentSceneLayer

    protected internal bool disableAddToRefreshQueue = true;
    #endregion

    #region constructors / finalizer
    [JsonConstructor]
    public SceneLayerPoint(SceneLayer matrix)
    {
        zOrder = 0;
        visible = true;
        parentSceneLayer = matrix;
    }
    
    internal SceneLayerPoint(SceneLayerPoint gridPoint, Point gridCoord)
    {
        parentSceneLayer = gridPoint.parentSceneLayer;
        gridCoordinates = gridCoord;
        disableAddToRefreshQueue = gridPoint.disableAddToRefreshQueue;
        zOrder = gridPoint.zOrder;
        visible = gridPoint.visible;
        frame = gridPoint.frame;
        enableFog = gridPoint.enableFog;
        Tag = gridPoint.Tag;
    }

    ~SceneLayerPoint()
    {
        Dispose();
    }
    #endregion

    #region public properties
    [JsonProperty]
    public virtual new Frame CurrentFrame
    {
        get { return frame; }
        set
        {
            if (!disableAddToRefreshQueue)
                base.frame = value;
            else
                base.CurrentFrame = value;
        }
    }

    [JsonIgnore]
    public virtual new int ZOrder
    {
        get { return zOrder; }
    }

    [JsonProperty]
    public bool DoNotRedrawChanges { get; set; }

    [JsonIgnore]
    public override Rectangle DrawLocation
    {
        get { return parentSceneLayer.CoordinateSystem.GetPxlRangeAtGridPt(this, true); }
    }

    [JsonIgnore]
    public override bool IsPositionFixed
    {
        get { return true; }
    }

    [JsonIgnore]
    public override PointF GridCoordinates
    {
        get { return (PointF)gridCoordinates; }
    }

    [JsonIgnore]
    public Point GridCoordinatesAbs
    {
        get { return gridCoordinates;}
    }

    [JsonIgnore]
    public override SceneLayer ParentGrid
    {
        get { return parentSceneLayer; }
    }

    [JsonProperty]
    public bool EnableAnimator
    {
        get { return (animator != null); }
        set
        {
            if (value == true)
            {
                if (animator == null)
                    animator = new Animator(this);
            }
            else
                if (animator != null)
                {
                    animator.Dispose();
                    animator = null;
                }
        }
    }
    #endregion

    #region IDisposable Members
    public new void Dispose()
    {
        base.Dispose();
    }
    #endregion
}
