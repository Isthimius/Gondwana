namespace Gondwana.Scenes.EventArgs;

public class ShowGridLinesChangedEventArgs : System.EventArgs
{
    public SceneLayer SceneLayer;
    public bool oldValue;
    public bool newValue;

    protected internal ShowGridLinesChangedEventArgs(SceneLayer sceneLayer, bool oldVal, bool newVal)
    {
        SceneLayer = sceneLayer;
        oldValue = oldVal;
        newValue = newVal;
    }
}