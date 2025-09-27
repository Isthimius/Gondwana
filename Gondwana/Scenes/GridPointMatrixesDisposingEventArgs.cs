namespace Gondwana.Scenes;

public delegate void SceneLayeresDisposingEventHandler(SceneLayeresDisposingEventArgs e);

public class SceneLayeresDisposingEventArgs : EventArgs
{
    public Scene Matrixes;

    protected internal SceneLayeresDisposingEventArgs(Scene matrixLayers)
    {
        Matrixes = matrixLayers;
    }
}