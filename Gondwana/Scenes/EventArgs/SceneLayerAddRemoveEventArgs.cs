namespace Gondwana.Scenes.EventArgs;

public delegate void SceneLayerAddRemoveHandler(SceneLayerAddRemoveEventArgs e);

public class SceneLayerAddRemoveEventArgs : System.EventArgs
{
    public Scene Layers;
    public SceneLayer Layer;

    protected internal SceneLayerAddRemoveEventArgs(Scene grids, SceneLayer grid)
    {
        Layers = grids;
        Layer = grid;
    }
}