using System.Numerics;

namespace Gondwana.Physics.Movement.Scripted;

/// <summary>
/// Represents the state and parameters of a scripted movement operation applied to an object.
/// Supports two movement modes: tween-based interpolation (<see cref="MovementScriptType.TweenTo"/>) 
/// and continuous directional movement (<see cref="MovementScriptType.Toward"/>).
/// </summary>
public struct ScriptedMovement
{
    /// <summary>
    /// The type of movement script currently active.
    /// Determines which fields are relevant and how the movement is calculated.
    /// See <see cref="MovementScriptType"/> for available movement modes.
    /// </summary>
    public MovementScriptType Type;

    /// <summary>
    /// The starting position captured when the movement script was initiated.
    /// Used primarily by <see cref="MovementScriptType.TweenTo"/> to calculate interpolation 
    /// between the origin and target over the specified duration.
    /// </summary>
    public Vector2 Origin;

    /// <summary>
    /// The target position or destination for the movement.
    /// For <see cref="MovementScriptType.TweenTo"/>, this is the final position after interpolation completes.
    /// For <see cref="MovementScriptType.Toward"/>, this is the position the object continuously moves toward.
    /// </summary>
    public Vector2 Target;

    /// <summary>
    /// The total duration of the movement in seconds.
    /// Only used by <see cref="MovementScriptType.TweenTo"/> to determine the interpolation timeframe.
    /// The tween completes when <see cref="ElapsedSec"/> reaches this value, regardless of distance traveled.
    /// </summary>
    public float DurationSec;

    /// <summary>
    /// The elapsed time in seconds since the movement script started.
    /// Only used by <see cref="MovementScriptType.TweenTo"/> to track progress through the tween.
    /// Normalized to [0,1] as (ElapsedSec / DurationSec) before applying the easing function.
    /// </summary>
    public float ElapsedSec;

    /// <summary>
    /// The movement speed in units per second.
    /// Only used by <see cref="MovementScriptType.Toward"/> for continuous directional movement.
    /// Determines how quickly the object moves toward the target position each frame.
    /// </summary>
    public float SpeedPerSec;

    /// <summary>
    /// The distance threshold for snapping to the target position.
    /// When the object is within this epsilon distance from the target, it snaps exactly to the target 
    /// and the movement is considered complete. Prevents endless micro-adjustments and floating-point precision issues.
    /// Used by both <see cref="MovementScriptType.TweenTo"/> and <see cref="MovementScriptType.Toward"/>.
    /// </summary>
    public float SnapEpsilon;

    /// <summary>
    /// Optional easing function applied to the normalized time value during interpolation.
    /// Only used by <see cref="MovementScriptType.TweenTo"/> to control the acceleration curve.
    /// If null, linear interpolation is used. See <see cref="EasingFunctions"/> for available easing curves.
    /// The function should accept a value in [0,1] and return a smoothed value in [0,1].
    /// </summary>
    public Func<float, float>? Easing;
}
