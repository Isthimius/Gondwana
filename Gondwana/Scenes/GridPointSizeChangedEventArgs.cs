namespace Gondwana.Scenes;

public delegate void GridPointSizeChangedEventHandler(GridPointSizeChangedEventArgs e);

public class GridPointSizeChangedEventArgs : EventArgs
{
    public SceneLayer layer;
    public int oldWidth;
    public int oldHeight;
    public int newWidth;
    public int newHeight;

    protected internal GridPointSizeChangedEventArgs(SceneLayer matrix, int oldW, int oldH, int newW, int newH)
    {
        layer = matrix;
        oldWidth = oldW;
        oldHeight = oldH;
        newWidth = newW;
        newHeight = newH;
    }
}
