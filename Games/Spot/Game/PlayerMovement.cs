namespace HWG.Spot.Game;

internal readonly record struct PlayerMovement(
    Player? Player,
    MovementType MovementType,
    int FromX,
    int FromY,
    int DestX,
    int DestY
);