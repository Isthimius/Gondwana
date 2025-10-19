using System.Numerics;
using Gondwana.Drawing.Sprites;
using Gondwana.Scenes;

namespace Gondwana.Movement;

public sealed class SpriteMovable : IMovableOnSceneLayer
{
    private readonly Sprite _s;
    private readonly SceneLayer _layer;

    public SpriteMovable(Sprite s, SceneLayer layer)
    {
        _s = s;
        _layer = layer;
    }

    public SceneLayer SceneLayer => _layer;                        // ← expose layer
    public CoordinateSpace PositionSpace => CoordinateSpace.Grid;
    public Vector2 GetPosition() => new(_s.GridCoordinates.X, _s.GridCoordinates.Y);
    public void SetPosition(Vector2 p) => _s.MoveSprite(p.X, p.Y);
}
