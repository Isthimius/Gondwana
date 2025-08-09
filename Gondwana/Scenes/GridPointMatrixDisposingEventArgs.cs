namespace Gondwana.Scenes;

public delegate void SceneLayerDisposingEventHandler(SceneLayerDisposingEventArgs e);

public class SceneLayerDisposingEventArgs : EventArgs
{
    public SceneLayer Matrix;

    protected internal SceneLayerDisposingEventArgs(SceneLayer matrix)
    {
        Matrix = matrix;
    }
}
