using System.Numerics;
using Gondwana.Drawing.Sprites;

namespace Gondwana.Movement;

public sealed class SpriteMovable : IMovable
{
    private readonly Sprite _s;
    public SpriteMovable(Sprite s) => _s = s;

    public MovementSpace PositionSpace => MovementSpace.Grid;

    public Vector2 GetPosition() => new(_s.GridCoordinates.X, _s.GridCoordinates.Y);
    public void SetPosition(Vector2 p) => _s.MoveSprite(p.X, p.Y);
}
