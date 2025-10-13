namespace Gondwana.Scenes.EventArgs;

public class ShowGridLinesChangedEventArgs : System.EventArgs
{
    public SceneLayer SceneLayer { get; set; }
    public bool OldValue { get; set; }
    public bool NewValue { get; set; }

    protected internal ShowGridLinesChangedEventArgs(SceneLayer sceneLayer, bool oldVal, bool newVal)
    {
        SceneLayer = sceneLayer;
        OldValue = oldVal;
        NewValue = newVal;
    }
}