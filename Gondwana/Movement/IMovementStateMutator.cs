using System.Numerics;

namespace Gondwana.Movement;

/// <summary>
/// Mutator API for adjusting motion parameters on a mover without exposing its internal state.
/// Keeps IMovable about position, and this interface about motion behavior.
/// </summary>
public interface IMovementStateMutator
{
    /// <summary>
    /// Gets a copy of the current <see cref="MovementState"/> representing
    /// this object's motion parameters and position. Changes made to the
    /// returned struct do not affect the internal state.
    /// Use Set... methods to modify motion.
    /// </summary>
    MovementState MovementState { get; }

    // Core kinematics
    void SetVelocity(Vector2 v);
    void SetAcceleration(Vector2 a);
    void StopMovement();                // sets v=a=0

    // Tuning
    void SetMaxSpeed(float? maxSpeed);  // null = no cap
    void SetLinearDamping(float dampingPerSec);
}
