namespace Gondwana.Scenes.EventArgs;

public class SceneLayerVisibleChangedEventArgs : System.EventArgs
{
    public SceneLayer SceneLayer { get; set; }
    public bool OldVisibleValue { get; set; }
    public bool NewVisibleValue { get; set; }

    protected internal SceneLayerVisibleChangedEventArgs(SceneLayer sceneLayer, bool oldValue, bool newValue)
    {
        SceneLayer = sceneLayer;
        OldVisibleValue = oldValue;
        NewVisibleValue = newValue;
    }
}