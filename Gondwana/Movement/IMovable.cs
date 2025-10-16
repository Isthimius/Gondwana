using System.Numerics;

namespace Gondwana.Movement;

public interface IMovable
{
    /// <summary>Which unit system this mover uses for its position.</summary>
    CoordinateSpace PositionSpace { get; }

    /// <summary>Get the current position in the mover's <see cref="PositionSpace"/>.</summary>
    Vector2 GetPosition();

    /// <summary>Set the position in the mover's <see cref="PositionSpace"/>.</summary>
    void SetPosition(Vector2 pos);
}
