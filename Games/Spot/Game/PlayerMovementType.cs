namespace HWG.Spot.Game;

internal readonly record struct PlayerMovementType(
    Player? Player,
    MovementType MovementType,
    int FromX,
    int FromY,
    int DestX,
    int DestY
);