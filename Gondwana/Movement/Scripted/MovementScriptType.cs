namespace Gondwana.Movement.Scripted;

/// <summary>
/// Defines the type of scripted movement behavior applied to an object.
/// Movement scripts control how an object moves over time, either through interpolation or continuous tracking.
/// </summary>
public enum MovementScriptType
{
    /// <summary>
    /// No movement script is active.
    /// The object is not being controlled by any scripted movement behavior and can move freely or remain stationary.
    /// This is the default state when no movement automation is applied.
    /// </summary>
    None,

    /// <summary>
    /// Tween-based movement to a target position or state over a fixed duration.
    /// The object interpolates smoothly from its current state to the target using an easing function.
    /// Movement completes after the specified duration, regardless of distance.
    /// Supports various easing curves (linear, quadratic, cubic, etc.) for different animation feels.
    /// </summary>
    TweenTo,

    /// <summary>
    /// Continuous movement toward a target position or object.
    /// The object moves in the direction of the target at a constant or variable speed.
    /// Movement continues until the object reaches the target or the script is cancelled.
    /// Useful for chase behaviors, homing projectiles, or AI pathfinding.
    /// </summary>
    Toward
}
