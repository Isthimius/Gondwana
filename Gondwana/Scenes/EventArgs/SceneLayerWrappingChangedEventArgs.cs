namespace Gondwana.Scenes.EventArgs;

public class SceneLayerWrappingChangedEventArgs : System.EventArgs
{
    public SceneLayer SceneLayer { get; set; }
    public bool OldHorizWrapping { get; set; }
    public bool NewHorizWrapping { get; set; }
    public bool OldVertiWrapping { get; set; }
    public bool NewVertiWrapping { get; set; }

    protected internal SceneLayerWrappingChangedEventArgs(SceneLayer _layer, bool _oldHoriz, bool _newHoriz, bool _oldVerti, bool _newVerti)
    {
        SceneLayer = _layer;
        OldHorizWrapping = _oldHoriz;
        NewHorizWrapping = _newHoriz;
        OldVertiWrapping = _oldVerti;
        NewVertiWrapping = _newVerti;
    }
}