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

    [IgnoreDataMember]
    internal SceneLayer ChildGrid;

    [DataMember]
    public PointF ParentAnchorGridPoint;

    [DataMember]
    public PointF ChildAnchorGridPoint;
}
