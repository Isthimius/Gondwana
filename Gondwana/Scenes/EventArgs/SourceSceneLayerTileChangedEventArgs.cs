using System.Drawing;

namespace Gondwana.Scenes.EventArgs;

public class SourceSceneLayerTileChangedEventArgs : System.EventArgs
{
    public SceneLayer SceneLayer { get; set; }
    public PointF OldPt { get; set; }
    public PointF NewPt { get; set; }

    protected internal SourceSceneLayerTileChangedEventArgs(SceneLayer sceneLayer, PointF oldP, PointF newP)
    {
        SceneLayer = sceneLayer;
        OldPt = oldP;
        NewPt = newP;
    }
}