using Gondwana.Common;
using Gondwana.Common.Drawing;
using Gondwana.Common.Drawing.Animation;
using System;
using System.Drawing;
using System.Runtime.Serialization;

namespace Gondwana.Grid;

/// <summary>
/// Represents the values stored at a single location on a GridPointMatrix
/// </summary>
[DataContract(IsReference = true)]
public class GridPoint : Tile, IDisposable
{
    #region private / internal fields
    [DataMember]
    internal GridPointMatrix parentGrid;

    [DataMember]
    internal Point gridCoordinates;         // each GridPoint knows its location in the array in ParentGrid

    protected internal bool disableAddToRefreshQueue = true;
    #endregion

    #region constructors / finalizer
    public GridPoint(GridPointMatrix matrix)
    {
        zOrder = 0;
        visible = true;
        parentGrid = matrix;
    }
    
    internal GridPoint(GridPoint gridPoint, Point gridCoord)
    {
        parentGrid = gridPoint.parentGrid;
        gridCoordinates = gridCoord;
        disableAddToRefreshQueue = gridPoint.disableAddToRefreshQueue;
        zOrder = gridPoint.zOrder;
        visible = gridPoint.visible;
        frame = gridPoint.frame;
        rasterOp = gridPoint.rasterOp;
        enableFog = gridPoint.enableFog;
        Tag = gridPoint.Tag;

        // associate new GridPoint (child, this) with existing GridPoint (parent)
        gridPoint.AddChild(this);
    }

    ~GridPoint()
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
                    foreach (GridPoint gridPt in childTiles)
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
                foreach (GridPoint gridPt in childTiles)
                    gridPt.DoNotRedrawChanges = value;
            }
        }
    }

    [IgnoreDataMember]
    public override Rectangle DrawLocation
    {
        get { return parentGrid.CoordinateSystem.GetPxlRangeAtGridPt(this, true); }
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
    public override GridPointMatrix ParentGrid
    {
        get { return parentGrid; }
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
