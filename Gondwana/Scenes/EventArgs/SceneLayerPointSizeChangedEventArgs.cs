namespace Gondwana.Scenes.EventArgs;

public delegate void GridPointSizeChangedEventHandler(SceneLayerPointSizeChangedEventArgs e);

public class SceneLayerPointSizeChangedEventArgs : EventArgs
{
    public SceneLayer layer;
    public int oldWidth;
    public int oldHeight;
    public int newWidth;
    public int newHeight;

    protected internal SceneLayerPointSizeChangedEventArgs(SceneLayer matrix, int oldW, int oldH, int newW, int newH)
    {
        layer = matrix;
        oldWidth = oldW;
        oldHeight = oldH;
        newWidth = newW;
        newHeight = newH;
    }
}