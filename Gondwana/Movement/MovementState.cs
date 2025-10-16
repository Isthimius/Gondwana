using System.Numerics;

namespace Gondwana.Movement;

public struct MovementState
{
    /// <summary>
    /// What unit system these values are expressed in.
    /// Grid = tile units; Pixel = screen pixels.
    /// </summary>
    public MovementSpace MovementSpace; // default Grid in your ctor/factory

    /// <summary>
    /// Position in current MotionSpace units (Grid or Pixel).
    /// </summary>
    public Vector2 Position;

    /// <summary>
    /// Velocity in current MotionSpace units per second.
    /// </summary>
    public Vector2 Velocity;

    /// <summary>
    /// Acceleration in current MotionSpace units per second^2.
    /// </summary>
    public Vector2 Acceleration;

    /// <summary>
    /// Max speed in current MotionSpace units per second (null = no cap).
    /// </summary>
    public float? MaxSpeed;

    /// <summary>
    /// Linear damping per second in [0..1]. 0 = no damping.
    /// Apply as v *= (1 - LinearDamping * dt) in the controller.
    /// </summary>
    public float LinearDamping;

    /// <summary>
    /// Optional wrapping (only meaningful for Grid space; ignored for Pixel).
    /// </summary>
    public bool WrapX, WrapY;

    public void ClampVelocity()
    {
        if (MaxSpeed is null) return;

        var v = Velocity;
        var len = v.Length();
        var max = MaxSpeed.Value;

        if (max > 0 && len > max)
            Velocity = v * (max / len);
    }

    /// <summary>
    /// Convenience factories to make intent explicit at call sites.
    /// </summary>
    public static MovementState Grid(Vector2 position, float linearDampening = 0f) => new()
    {
        MovementSpace = MovementSpace.Grid,
        Position = position,
        LinearDamping = linearDampening
    };

    public static MovementState Pixel(Vector2 position, float linearDampening = 0f) => new()
    {
        MovementSpace = MovementSpace.Pixel,
        Position = position,
        LinearDamping = linearDampening
    };
}
