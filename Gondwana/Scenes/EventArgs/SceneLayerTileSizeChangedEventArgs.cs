namespace Gondwana.Scenes.EventArgs;

public class SceneLayerTileSizeChangedEventArgs : System.EventArgs
{
    public SceneLayer SceneLayer { get; set; }
    public int OldWidth { get; set; }
    public int OldHeight { get; set; }
    public int NewWidth { get; set; }
    public int NewHeight { get; set; }

    protected internal SceneLayerTileSizeChangedEventArgs(SceneLayer sceneLayer, int oldW, int oldH, int newW, int newH)
    {
        SceneLayer = sceneLayer;
        OldWidth = oldW;
        OldHeight = oldH;
        NewWidth = newW;
        NewHeight = newH;
    }
}