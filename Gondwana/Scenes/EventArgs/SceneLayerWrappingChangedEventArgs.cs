namespace Gondwana.Scenes.EventArgs;

public delegate void SceneLayerWrappingChangedEventHandler(SceneLayerWrappingChangedEventArgs e);

public class SceneLayerWrappingChangedEventArgs : System.EventArgs
{
    public SceneLayer layer;
    public bool oldHorizWrapping;
    public bool newHorizWrapping;
    public bool oldVertiWrapping;
    public bool newVertiWrapping;

    protected internal SceneLayerWrappingChangedEventArgs(SceneLayer _layer, bool _oldHoriz, bool _newHoriz, bool _oldVerti, bool _newVerti)
    {
        layer = _layer;
        oldHorizWrapping = _oldHoriz;
        newHorizWrapping = _newHoriz;
        oldVertiWrapping = _oldVerti;
        newHorizWrapping = _newVerti;
    }
}