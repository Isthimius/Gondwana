using System.Numerics;
using Gondwana.Movement.Scripted;

namespace Gondwana.Movement;

/// <summary>
/// Represents the current motion state of an <see cref="IMovable"/> object, including velocity, acceleration,
/// damping, and active scripted movements. This structure is used by <see cref="MovementController"/> to
/// track and manage both physics-based (integrated) and scripted motion behaviors.
/// </summary>
public struct MovementState
{
    /// <summary>
    /// What unit system these values are expressed in.
    /// Grid = tile units; Pixel = screen pixels.
    /// This may differ from the IMovable.PositionSpace in some cases
    /// (e.g., a Pixel-based mover following Grid-based Sprite, etc.).
    /// </summary>
    public MovementSpace MovementSpace { get; private set; }

    /// <summary>
    /// Velocity in current MotionSpace units per second.
    /// </summary>
    public Vector2 Velocity { get; internal set; }

    /// <summary>
    /// Acceleration in current MotionSpace units per second^2.
    /// </summary>
    public Vector2 Acceleration { get; internal set; }

    /// <summary>
    /// Max speed in current MotionSpace units per second (null = no cap).
    /// </summary>
    public float? MaxSpeed { get; internal set; }

    /// <summary>
    /// Linear damping per second in [0..1]. 0 = no damping.
    /// Apply as v *= (1 - LinearDamping * dt) in the controller.
    /// Only affects integrated movement (not scripted).
    /// </summary>
    public float LinearDamping { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether the object is in motion.
    /// </summary>
    public readonly bool HasMotion => Acceleration != Vector2.Zero || Velocity != Vector2.Zero;

    /// <summary>
    /// Active scripted movement command (TweenTo, Toward, etc.).
    /// Default is <see cref="MovementScriptType.None"/>.
    /// </summary>
    internal ScriptedMovement Script;

    /// <summary>
    /// returns a MovementState initialized for SceneLayer (Grid) coordinates
    /// </summary>
    /// <param name="linearDampening">The linear damping factor to apply to the movement. Defaults to <see langword="0f"/> if not specified.</param>
    /// <returns>A new <see cref="MovementState"/> instance configured with the specified position, linear damping, and a
    /// coordinate space of <see cref="MovementSpace.Grid"/>.</returns>
    internal static MovementState ForSceneLayer(Vector2 position, float linearDampening = 0f) => new()
    {
        MovementSpace = MovementSpace.Grid,
        LinearDamping = linearDampening
    };

    /// <summary>
    /// Creates a new <see cref="MovementState"/> instance with the specified position and optional linear damping,
    /// using pixel-based coordinates.
    /// </summary>
    /// <param name="position">The position of the movement state in pixel-based coordinates.</param>
    /// <param name="linearDampening">The linear damping factor to apply to the movement. Defaults to <see langword="0f"/> if not specified.</param>
    /// <returns>A new <see cref="MovementState"/> instance configured with the specified position, linear damping, and a
    /// coordinate space of <see cref="MovementSpace.Pixel"/>.</returns>
    internal static MovementState ForPixel(Vector2 position, float linearDampening = 0f) => new()
    {
        MovementSpace = MovementSpace.Pixel,
        LinearDamping = linearDampening
    };
}
