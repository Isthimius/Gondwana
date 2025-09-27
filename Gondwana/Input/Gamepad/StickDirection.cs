namespace Gondwana.Input.Gamepad;

[Flags]
public enum StickDirection
{
    None = 0,
    Up = 1 << 0,
    Down = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3
}