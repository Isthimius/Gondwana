using System.Drawing;
using System.Runtime.Serialization;

namespace Gondwana.Scenes;

[DataContract(IsReference = true)]
public class SceneLayerScrollBinding
{
    internal static List<SceneLayerScrollBinding> _allScrollBindings =
        new List<SceneLayerScrollBinding>();

    #region ctor
    public SceneLayerScrollBinding()
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
    public SceneLayer ParentGrid;

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
                ParentGrid = SceneLayer.GetSceneLayerByID(value);
        }
    }

    [IgnoreDataMember]
    internal SceneLayer ChildGrid;

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
                ChildGrid = SceneLayer.GetSceneLayerByID(value);
        }
    }

    [DataMember]
    public PointF ParentAnchorGridPoint;

    [DataMember]
    public PointF ChildAnchorGridPoint;
}
