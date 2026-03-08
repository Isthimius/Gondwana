namespace Gondwana.Input.Gamepad;

/// <summary>
/// Represents the state of an analog stick on a gamepad, including both normalized floating-point
/// values for convenient use in game logic and raw integer values from the underlying input system.
/// This immutable struct provides methods for deadzone processing, direction detection, and magnitude calculation.
/// </summary>
public readonly struct GamepadStickState
{
    /// <summary>
    /// Gets the normalized horizontal position of the analog stick in the range [-1, 1],
    /// where -1 represents full left, 0 represents center, and 1 represents full right.
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Gets the normalized vertical position of the analog stick in the range [-1, 1],
    /// where -1 represents full down, 0 represents center, and 1 represents full up.
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Raw horizontal value before normalization (e.g., from XInput or SDL2)
    /// </summary>
    public int RawX { get; }

    /// <summary>
    /// Raw vertical value before normalization (e.g., from XInput or SDL2)
    /// </summary>
    public int RawY { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GamepadStickState"/> struct with the specified
    /// normalized and optional raw values.
    /// </summary>
    /// <param name="x">
    /// The normalized horizontal position in the range [-1, 1], where -1 is full left,
    /// 0 is center, and 1 is full right.
    /// </param>
    /// <param name="y">
    /// The normalized vertical position in the range [-1, 1], where -1 is full down,
    /// 0 is center, and 1 is full up.
    /// </param>
    /// <param name="rawX">
    /// The raw horizontal value from the underlying input system. Default is 0.
    /// </param>
    /// <param name="rawY">
    /// The raw vertical value from the underlying input system. Default is 0.
    /// </param>
    public GamepadStickState(float x, float y, int rawX = 0, int rawY = 0)
    {
        X = x;
        Y = y;
        RawX = rawX;
        RawY = rawY;
    }

    /// <summary>
    /// Creates a <see cref="GamepadStickState"/> instance from raw 16-bit signed integer values.
    /// </summary>
    /// <remarks>The method normalizes the raw input values to a floating-point range of [-1, 1]  to represent
    /// the stick's position, while preserving the original raw values for reference.</remarks>
    /// <param name="rawX">The raw X-axis value, ranging from -32768 to 32767.</param>
    /// <param name="rawY">The raw Y-axis value, ranging from -32768 to 32767.</param>
    /// <returns>A <see cref="GamepadStickState"/> representing the normalized stick position,  with X and Y values clamped to
    /// the range [-1, 1], and the original raw values.</returns>
    public static GamepadStickState FromRaw16(int rawX, int rawY)
    {
        const float range = 32767f; // signed range: -32768 to 32767
        float normX = Math.Clamp(rawX / range, -1f, 1f);
        float normY = Math.Clamp(rawY / range, -1f, 1f);
        return new GamepadStickState(normX, normY, rawX, rawY);
    }

    /// <summary>
    /// Creates a <see cref="GamepadStickState"/> instance from raw 16-bit unsigned integer values.
    /// </summary>
    /// <remarks>The raw input values are normalized to the range [-1, 1] based on the assumption that the
    /// midpoint (32768) represents the neutral position, and the range extends from 0 to 65535.</remarks>
    /// <param name="rawX">The raw X-axis value, represented as an unsigned 16-bit integer.</param>
    /// <param name="rawY">The raw Y-axis value, represented as an unsigned 16-bit integer.</param>
    /// <returns>A <see cref="GamepadStickState"/> instance with normalized X and Y values in the range [-1, 1], along with the
    /// original raw input values.</returns>
    public static GamepadStickState FromRawUnsigned16(ushort rawX, ushort rawY)
    {
        float normX = (rawX - 32768f) / 32767f;
        float normY = (rawY - 32768f) / 32767f;
        return new GamepadStickState(normX, normY, rawX, rawY);
    }

    /// <summary>
    /// Returns the magnitude (0 to 1) of the stick position.
    /// </summary>
    public float Magnitude => MathF.Sqrt(X * X + Y * Y);

    /// <summary>
    /// Returns the angle (in radians) relative to (1, 0).
    /// </summary>
    public float Angle => MathF.Atan2(Y, X);

    /// <summary>
    /// Returns true if the stick is pushed beyond the threshold.
    /// </summary>
    public bool IsEngaged(float threshold = 0.15f) => Magnitude >= threshold;

    /// <summary>
    /// Returns the primary stick direction(s) based on angle and threshold.
    /// </summary>
    public StickDirection Direction(float threshold = 0.15f)
    {
        if (!IsEngaged(threshold)) return StickDirection.None;

        var dir = StickDirection.None;
        if (Y >= threshold) dir |= StickDirection.Up;
        if (Y <= -threshold) dir |= StickDirection.Down;
        if (X <= -threshold) dir |= StickDirection.Left;
        if (X >= threshold) dir |= StickDirection.Right;

        return dir;
    }

    /// <summary>
    /// Returns a new stick state with a deadzone applied.
    /// </summary>
    public GamepadStickState WithDeadzone(float threshold = 0.15f)
    {
        return IsEngaged(threshold) ? this : new GamepadStickState(0, 0, RawX, RawY);
    }

    /// <summary>
    /// Returns a string representation of the normalized stick position showing the X and Y values
    /// formatted to two decimal places in the format "(X, Y)".
    /// </summary>
    /// <returns>A string in the format "(X, Y)" where X and Y are formatted to two decimal places.</returns>
    public override string ToString() => $"({X:0.00}, {Y:0.00})";
}