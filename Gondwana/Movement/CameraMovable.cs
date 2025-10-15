using System.Numerics;
using Gondwana.Scenes;

namespace Gondwana.Movement;

public sealed class CameraMovable : IMovable
{
    private readonly SceneLayer _sceneLayer;
    public CameraMovable(SceneLayer sceneLayer) => _sceneLayer = sceneLayer;

    public Vector2 GetGridPosition()
        => new(_sceneLayer.SourceSceneLayerTile.X, _sceneLayer.SourceSceneLayerTile.Y);

    public void SetGridPosition(System.Numerics.Vector2 p)
        => _sceneLayer.SourceSceneLayerTile = new System.Drawing.PointF(p.X, p.Y);
}
