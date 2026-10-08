using System.Numerics;

namespace Gondwana.Physics.Movement;

/// <summary>
/// Defines an object that can be moved within a specific coordinate space.
/// </summary>
/// <remarks>The <see cref="IMovable"/> interface provides methods to retrieve and update the position of an
/// object in a defined <see cref="MovementSpace"/>. Implementations of this interface are expected to handle
/// position-related operations consistently within the specified coordinate system.</remarks>
public interface IMovable
{
    /// <summary>Which unit system this mover uses for its position.</summary>
    MovementSpace PositionSpace { get; }

    /// <summary>Get the current position in the mover's <see cref="PositionSpace"/>.</summary>
    /// <returns>The sprite position in scene-layer grid coordinates.</returns>
    Vector2 GetPosition();

    /// <summary>Set the position in the mover's <see cref="PositionSpace"/>.</summary>
    /// <param name="pos">The new position in scene-layer grid coordinates.</param>
    void SetPosition(Vector2 pos);
}
