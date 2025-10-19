using System.Numerics;

namespace Gondwana.Movement;

public struct MovementState
{
    /// <summary>
    /// What unit system these values are expressed in.
    /// Grid = tile units; Pixel = screen pixels.
    /// </summary>
    public CoordinateSpace MovementSpace { get; private set; }

    /// <summary>
    /// Position in current MotionSpace units (Grid or Pixel).
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// Velocity in current MotionSpace units per second.
    /// </summary>
    public Vector2 Velocity { get; set; }

    /// <summary>
    /// Acceleration in current MotionSpace units per second^2.
    /// </summary>
    public Vector2 Acceleration { get; set; }

    /// <summary>
    /// Max speed in current MotionSpace units per second (null = no cap).
    /// </summary>
    public float? MaxSpeed { get; set; }

    /// <summary>
    /// Linear damping per second in [0..1]. 0 = no damping.
    /// Apply as v *= (1 - LinearDamping * dt) in the controller.
    /// </summary>
    public float LinearDamping { get; set; }

    /// <summary>
    /// Optional wrapping (only meaningful for Grid space; ignored for Pixel).
    /// </summary>
    public bool WrapX { get; set; }

    /// <summary>
    /// Optional wrapping (only meaningful for Grid space; ignored for Pixel).
    /// </summary>
    public bool WrapY { get; set; }

    /// <summary>
    /// Gets a value indicating whether the object is in motion.
    /// </summary>
    public bool HasMotion => Acceleration != Vector2.Zero || Velocity != Vector2.Zero;

    public void Stop()
    {
        Velocity = Vector2.Zero;
        Acceleration = Vector2.Zero;
    }

    /// <summary>
    /// Limits the current velocity to the maximum speed, if specified.
    /// </summary>
    /// <remarks>If <see cref="MaxSpeed"/> is not set, the method does nothing. If the current velocity
    /// exceeds the  maximum speed, it is scaled down proportionally to ensure its magnitude does not exceed the
    /// maximum.</remarks>
    internal void ClampVelocity()
    {
        if (MaxSpeed is null)
            return;

        var v = Velocity;
        var speed = v.Length();
        var max = MaxSpeed.Value;

        if (max > 0 && speed > max)
            Velocity = v * (max / speed);
    }

    /// <summary>
    /// returns a MovementState initialized for SceneLayer (Grid) coordinates
    /// </summary>
    /// <param name="linearDampening">The linear damping factor to apply to the movement. Defaults to <see langword="0f"/> if not specified.</param>
    /// <returns>A new <see cref="MovementState"/> instance configured with the specified position, linear damping, and a
    /// coordinate space of <see cref="CoordinateSpace.Grid"/>.</returns>
    public static MovementState ForSceneLayer(Vector2 position, float linearDampening = 0f) => new()
    {
        MovementSpace = CoordinateSpace.Grid,
        Position = position,
        LinearDamping = linearDampening
    };

    /// <summary>
    /// Creates a new <see cref="MovementState"/> instance with the specified position and optional linear damping,
    /// using pixel-based coordinates.
    /// </summary>
    /// <param name="position">The position of the movement state in pixel-based coordinates.</param>
    /// <param name="linearDampening">The linear damping factor to apply to the movement. Defaults to <see langword="0f"/> if not specified.</param>
    /// <returns>A new <see cref="MovementState"/> instance configured with the specified position, linear damping, and a
    /// coordinate space of <see cref="CoordinateSpace.Pixel"/>.</returns>
    public static MovementState ForPixel(Vector2 position, float linearDampening = 0f) => new()
    {
        MovementSpace = CoordinateSpace.Pixel,
        Position = position,
        LinearDamping = linearDampening
    };
}
