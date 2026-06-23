using Gondwana.Physics.Movement;
using System.Drawing;
using System.Numerics;

namespace Gondwana.Movement;

public sealed partial class MovementController
{
    /// <summary>
    /// Sets the velocity of the object.
    /// </summary>
    /// <remarks>This method cancels any ongoing scripts before applying the new velocity.</remarks>
    /// <param name="velocity">The new velocity to apply, represented as a <see cref="Vector2"/>.</param>
    public void SetVelocity(Vector2 velocity)
    {
        CancelScript();
        _state.Velocity = velocity;
    }

    /// <summary>
    /// Sets the acceleration of the object.
    /// </summary>
    /// <remarks>This method cancels any ongoing scripts before updating the acceleration.</remarks>
    /// <param name="acceleration">The new acceleration to apply, represented as a <see cref="Vector2"/></param>
    public void SetAcceleration(Vector2 acceleration)
    {
        CancelScript();
        _state.Acceleration = acceleration;
    }

    /// <summary>
    /// Sets the maximum speed for the current state. Only applies to integrated movement.
    /// </summary>
    /// <param name="maxSpeed">The maximum speed to set, in units appropriate to the context. Specify <see langword="null"/> to remove the
    /// speed limit.</param>
    public void SetMaxSpeed(float? maxSpeed)
    {
        _state.MaxSpeed = maxSpeed;
    }

    /// <summary>
    /// Sets the linear damping value, which determines the rate at which linear motion is reduced over time.
    /// </summary>
    /// <remarks>Linear damping is used to simulate the gradual reduction of velocity in a system, such as due
    /// to friction or drag. If the specified value is negative, it will be clamped to zero.</remarks>
    /// <param name="dampingPerSec">The damping value per second. Must be a non-negative value.</param>
    public void SetLinearDamping(float dampingPerSec)
    {
        _state.LinearDamping = MathF.Max(0f, dampingPerSec);
    }

    private bool AdvanceIntegrated(float dt)
    {
        if (_state.HasMotion)
        {
            Step(dt);       // physics (accel/vel/damping/wrap/space-convert)
            return true;
        }

        return false;
    }

    /// <summary>
    /// handle physics-based (velocity/acceleration, damping) movement step over dt seconds
    /// </summary>
    private void Step(float dt)
    {
        // integrate kinematics in the mover's own space
        _state.Velocity += _state.Acceleration * dt;
        ClampVelocity();

        // exponential damping (frame-rate independent)
        if (_state.LinearDamping > 0f)
            _state.Velocity *= MathF.Exp(-_state.LinearDamping * dt);

        // kill tiny velocities when no accel
        const float StopEpsilon = 0.01f;
        if (_state.Acceleration == Vector2.Zero &&
            _state.Velocity.LengthSquared() < StopEpsilon * StopEpsilon)
            _state.Velocity = Vector2.Zero;

        // move in the mover's space
        var pos = _mover.GetPosition();
        var newPos = pos + _state.Velocity * dt;

        // optional wrapping if mover lives in Grid space
        if (_mover.PositionSpace == MovementSpace.Grid && (WrapX || WrapY))
        {
            if (_coords is null || _sceneLayer is null)
                throw new InvalidOperationException("Grid wrapping requires coordinates/layer.");

            var wrapped = _coords.FindEquivalentSceneLayerCoordinates(
                new PointF(newPos.X, newPos.Y),
                _sceneLayer.GridColumnCount - 1, _sceneLayer.GridRowCount - 1);

            float newX = newPos.X;
            float newY = newPos.Y;

            if (WrapX)
                newX = wrapped.X;

            if (WrapY)
                newY = wrapped.Y;

            newPos = new Vector2(newX, newY);
        }

        _mover.SetPosition(newPos);
    }

    /// <summary>
    /// Zeroes out velocity components along the specified axes.
    /// Used by collision resolution to cancel movement into a surface while
    /// preserving motion along unblocked axes (e.g. wall-sliding in a platformer).
    /// Does not cancel scripted movement.
    /// </summary>
    /// <param name="zeroX">When <see langword="true"/>, zeroes the horizontal velocity component.</param>
    /// <param name="zeroY">When <see langword="true"/>, zeroes the vertical velocity component.</param>
    internal void ZeroVelocityComponent(bool zeroX, bool zeroY)
    {
        if (!zeroX && !zeroY)
            return;

        var v = _state.Velocity;
        _state.Velocity = new Vector2(zeroX ? 0f : v.X, zeroY ? 0f : v.Y);
    }

    /// <summary>
    /// Limits the current velocity to the maximum speed, if specified.
    /// </summary>
    /// <remarks>If <see cref="MaxSpeed"/> is not set, the method does nothing. If the current velocity
    /// exceeds the  maximum speed, it is scaled down proportionally to ensure its magnitude does not exceed the
    /// maximum.</remarks>
    private void ClampVelocity()
    {
        if (_state.MaxSpeed is null)
            return;

        var v = _state.Velocity;
        var speed = v.Length();
        var max = _state.MaxSpeed.Value;

        if (max > 0 && speed > max)
            _state.Velocity = v * (max / speed);
    }
}
