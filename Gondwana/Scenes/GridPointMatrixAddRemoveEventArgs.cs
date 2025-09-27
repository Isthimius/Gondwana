namespace Gondwana.Scenes;

public delegate void SceneLayerAddRemoveHandler(SceneLayerAddRemoveEventArgs e);

public class SceneLayerAddRemoveEventArgs : EventArgs
{
    public Scene Layers;
    public SceneLayer Layer;

    protected internal SceneLayerAddRemoveEventArgs(Scene grids, SceneLayer grid)
    {
        Layers = grids;
        Layer = grid;
    }
}