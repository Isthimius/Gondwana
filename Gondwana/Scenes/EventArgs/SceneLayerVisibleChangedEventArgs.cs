namespace Gondwana.Scenes.EventArgs;

public class SceneLayerVisibleChangedEventArgs : System.EventArgs
{
    public SceneLayer SceneLayer;
    public bool oldVisibleValue;
    public bool newVisibleValue;

    protected internal SceneLayerVisibleChangedEventArgs(SceneLayer sceneLayer, bool oldValue, bool newValue)
    {
        SceneLayer = sceneLayer;
        oldVisibleValue = oldValue;
        newVisibleValue = newValue;
    }
}