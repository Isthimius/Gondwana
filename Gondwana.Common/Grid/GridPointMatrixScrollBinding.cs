using System.Runtime.Serialization;

namespace Gondwana.Grid;

[DataContract(IsReference = true)]
public class GridPointMatrixScrollBinding
{
    internal static List<GridPointMatrixScrollBinding> _allScrollBindings =
        new List<GridPointMatrixScrollBinding>();

    #region ctor
    public GridPointMatrixScrollBinding()
    {
        _allScrollBindings.Add(this);
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        _allScrollBindings.Add(this);
    }
    #endregion

    [IgnoreDataMember]
    public GridPointMatrix ParentGrid;

    [DataMember]
    private string ParentGridId
    {
        get
        {
            if (ParentGrid == null)
                return string.Empty;
            else
                return ParentGrid.ID;
        }
        set
        {
            if (string.IsNullOrEmpty(value))
                ParentGrid = null;
            else
                ParentGrid = GridPointMatrix.GetGridPointMatrixByID(value);
        }
    }

    [IgnoreDataMember]
    internal GridPointMatrix ChildGrid;

    [DataMember]
    private string ChildGridId
    {
        get
        {
            if (ChildGrid == null)
                return string.Empty;
            else
                return ChildGrid.ID;
        }
        set
        {
            if (string.IsNullOrEmpty(value))
                ChildGrid = null;
            else
                ChildGrid = GridPointMatrix.GetGridPointMatrixByID(value);
        }
    }

    [DataMember]
    public PointF ParentAnchorGridPoint;

    [DataMember]
    public PointF ChildAnchorGridPoint;
}
