using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation;
using Newtonsoft.Json;

namespace Gondwana.Scenes;

/// <summary>
/// Represents the values stored at a single location on a SceneLayer
/// </summary>
[JsonObject(IsReference = true)]
public class SceneLayerTile : Tile
{
    #region private / internal fields

    [JsonProperty]
    internal SceneLayer parentSceneLayer;

    /// <summary>
    /// each SceneLayerTile knows its location in the array in parentSceneLayer;
    /// this is the position in the SceneLayer array, not pixel coordinates
    /// </summary>
    [JsonProperty]
    internal Point sceneLayerCoordinates;

    #endregion private / internal fields

    #region constructors / finalizer

    [JsonConstructor]
    public SceneLayerTile(SceneLayer matrix)
    {
        zOrder = 0;
        visible = true;
        parentSceneLayer = matrix;
    }

    internal SceneLayerTile(SceneLayerTile sceneLayerTile, Point gridCoord)
    {
        parentSceneLayer = sceneLayerTile.parentSceneLayer;
        sceneLayerCoordinates = gridCoord;
        zOrder = sceneLayerTile.zOrder;
        visible = sceneLayerTile.visible;
        frame = sceneLayerTile.frame;
        enableFog = sceneLayerTile.enableFog;
        Tag = sceneLayerTile.Tag;
    }

    ~SceneLayerTile()
    {
        Dispose();
    }

    #endregion constructors / finalizer

    #region public properties

    [JsonIgnore]
    public virtual new int ZOrder => zOrder;

    [JsonIgnore]
    public override Rectangle DrawLocation => parentSceneLayer.CoordinateSystem.GetPixelRangeAtLayerPoint(this, true);

    [JsonIgnore]
    public override bool IsPositionFixed => true;

    [JsonIgnore]
    public override PointF GridCoordinates => (PointF)sceneLayerCoordinates;

    [JsonIgnore]
    public Point GridCoordinatesAbs => sceneLayerCoordinates;

    [JsonIgnore]
    public override SceneLayer ParentGrid => parentSceneLayer;

    [JsonProperty]
    public bool EnableAnimator
    {
        get { return (animator != null); }
        set
        {
            if (value)
            {
                if (animator == null)
                    animator = new Animator(this);

                return;
            }

            animator?.Dispose();
            animator = null;
        }
    }

    #endregion public properties
}