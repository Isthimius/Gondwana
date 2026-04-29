namespace Gondwana.Demos.SpotGL.Game;

internal readonly record struct PlayerMovement(
    Player Player,
    MovementType MovementType,
    SpotGameField.Cell FromCell,
    int FromX,
    int FromY,
    int DestX,
    int DestY
);