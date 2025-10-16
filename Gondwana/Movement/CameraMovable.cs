using System.Drawing;
using System.Numerics;
using Gondwana.Scenes;

namespace Gondwana.Movement;

public sealed class CameraMovable : IMovable
{
    private readonly SceneLayer _layer;
    public CameraMovable(SceneLayer layer) => _layer = layer;

    public MovementSpace PositionSpace => MovementSpace.Grid;

    public Vector2 GetPosition()
        => new(_layer.SourceSceneLayerTile.X, _layer.SourceSceneLayerTile.Y);

    public void SetPosition(Vector2 pos)
        => _layer.SourceSceneLayerTile = new PointF(pos.X, pos.Y);
}
