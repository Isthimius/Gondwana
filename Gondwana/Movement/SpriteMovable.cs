using System.Numerics;
using Gondwana.Drawing.Sprites;

namespace Gondwana.Movement;

public sealed class SpriteMovable : IMovable
{
    private readonly Sprite _sprite;
    public SpriteMovable(Sprite sprite) => _sprite = sprite;

    public Vector2 GetGridPosition()
        => new(_sprite.GridCoordinates.X, _sprite.GridCoordinates.Y);

    public void SetGridPosition(System.Numerics.Vector2 p)
        => _sprite.MoveSprite(p.X, p.Y);
}
