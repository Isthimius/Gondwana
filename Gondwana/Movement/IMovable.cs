using System.Numerics;

namespace Gondwana.Movement;

public interface IMovable
{
    // Always GET/SET in GRID space (floats). Adapters do conversion.
    Vector2 GetGridPosition();
    void SetGridPosition(Vector2 gridPos);
}
