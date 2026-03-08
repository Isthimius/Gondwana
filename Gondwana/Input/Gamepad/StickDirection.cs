namespace Gondwana.Input.Gamepad;

/// <summary>
/// Represents the directional state of an analog stick on a gamepad. This is a flags enumeration,
/// allowing multiple directions to be combined to represent diagonal movements (e.g., Up | Right for upper-right).
/// </summary>
[Flags]
public enum StickDirection
{
    /// <summary>
    /// No direction; the analog stick is in the neutral position or within the deadzone threshold.
    /// </summary>
    None = 0,

    /// <summary>
    /// The analog stick is pushed upward (positive Y-axis direction).
    /// Can be combined with <see cref="Left"/> or <see cref="Right"/> for diagonal directions.
    /// </summary>
    Up = 1 << 0,

    /// <summary>
    /// The analog stick is pushed downward (negative Y-axis direction).
    /// Can be combined with <see cref="Left"/> or <see cref="Right"/> for diagonal directions.
    /// </summary>
    Down = 1 << 1,

    /// <summary>
    /// The analog stick is pushed to the left (negative X-axis direction).
    /// Can be combined with <see cref="Up"/> or <see cref="Down"/> for diagonal directions.
    /// </summary>
    Left = 1 << 2,

    /// <summary>
    /// The analog stick is pushed to the right (positive X-axis direction).
    /// Can be combined with <see cref="Up"/> or <see cref="Down"/> for diagonal directions.
    /// </summary>
    Right = 1 << 3
}