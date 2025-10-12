using System.Drawing;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Gondwana.Scenes;

[JsonObject(IsReference = true)]
public class SceneLayerScrollBinding
{
    internal readonly static List<SceneLayerScrollBinding> _allScrollBindings =
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

    #endregion ctor

    [JsonIgnore]
    public SceneLayer ParentSceneLayer;

    [JsonIgnore]
    internal SceneLayer ChildGrid;

    [JsonProperty]
    public PointF ParentAnchorSceneLayerTile;

    [JsonProperty]
    public PointF ChildAnchorSceneLayerTile;
}