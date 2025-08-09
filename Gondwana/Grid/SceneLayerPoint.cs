using Gondwana.Drawing;
using Gondwana.Drawing.Animation;
using System.Drawing;
using System.Runtime.Serialization;

namespace Gondwana.Grid;

/// <summary>
/// Represents the values stored at a single location on a SceneLayer
/// </summary>
[DataContract(IsReference = true)]
public class SceneLayerPoint : Tile, IDisposable
{
    #region private / internal fields
    [DataMember]
    internal SceneLayer parentSceneLayer;

    [DataMember]
    internal Point gridCoordinates;         // each GridPoint knows its location in the array in ParentGrid

    protected internal bool disableAddToRefreshQueue = true;
    #endregion

    #region constructors / finalizer
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

        // associate new GridPoint (child, this) with existing GridPoint (parent)
        gridPoint.AddChild(this);
    }

    ~SceneLayerPoint()
    {
        Dispose();
    }
    #endregion

    #region public properties
    [DataMember]
    public virtual new Frame CurrentFrame
    {
        get { return frame; }
        set
        {
            if (disableAddToRefreshQueue == false)
                base.frame = value;

                if (childTiles != null)
                {
                    foreach (SceneLayerPoint gridPt in childTiles)
                        gridPt.frame = value;
                }
            else
                base.CurrentFrame = value;
        }
    }

    [IgnoreDataMember]
    public virtual new int ZOrder
    {
        get { return zOrder; }
    }

    [DataMember]
    public bool DoNotRedrawChanges
    {
        get { return disableAddToRefreshQueue; }
        set
        {
            disableAddToRefreshQueue = value;

            if (childTiles != null)
            {
                foreach (SceneLayerPoint gridPt in childTiles)
                    gridPt.DoNotRedrawChanges = value;
            }
        }
    }

    [IgnoreDataMember]
    public override Rectangle DrawLocation
    {
        get { return parentSceneLayer.CoordinateSystem.GetPxlRangeAtGridPt(this, true); }
    }

    [IgnoreDataMember]
    public override bool IsPositionFixed
    {
        get { return true; }
    }

    [IgnoreDataMember]
    public override PointF GridCoordinates
    {
        get { return (PointF)gridCoordinates; }
    }

    [IgnoreDataMember]
    public Point GridCoordinatesAbs
    {
        get { return gridCoordinates;}
    }

    [IgnoreDataMember]
    public override SceneLayer ParentGrid
    {
        get { return parentSceneLayer; }
    }

    [DataMember]
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
