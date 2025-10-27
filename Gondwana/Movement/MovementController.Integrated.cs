using System.Drawing;
using System.Numerics;

namespace Gondwana.Movement;

public sealed partial class MovementController
{
    public void Step(float dt)
    {
        if (_state.HasMotion)
            Step(_target, ref _state, dt);
    }  // integrates & applies via IMovable

    /// <summary>
    /// handle physics-based (velocity/acceleration, damping) movement step over dt seconds
    /// </summary>
    internal void Step(IMovable mover, ref MovementState s, float dt)
    {
        // integrate
        s.Velocity += s.Acceleration * dt;
        ClampVelocity(ref s);

        /// Linear damping per second in [0..∞). Applied exponentially in the controller:
        /// v *= Exp(-LinearDamping * dt). Use 0 for no damping.
        if (s.LinearDamping > 0f)
            s.Velocity *= MathF.Exp(-s.LinearDamping * dt);

        // after exp damping
        const float StopEpsilon = 0.01f; // px/s for pixel; fine for grid too
        if (s.Acceleration == Vector2.Zero &&
            s.Velocity.LengthSquared() < StopEpsilon * StopEpsilon)
            s.Velocity = Vector2.Zero;

        s.Position += s.Velocity * dt;

        // optional grid wrapping needs coords/layer
        if (s.MovementSpace == CoordinateSpace.Grid && (s.WrapX || s.WrapY))
        {
            if (_coords is null || _sceneLayer is null)
                throw new InvalidOperationException("Grid wrapping requires coordinates/layer.");

            var wrapped = _coords.FindEquivalentSceneLayerCoordinates(
                new PointF(s.Position.X, s.Position.Y),
                _sceneLayer.GridColumnCount - 1, _sceneLayer.GridRowCount - 1);

            s.Position = new Vector2(wrapped.X, wrapped.Y);
        }

        // same-space: no dependencies
        if (s.MovementSpace == mover.PositionSpace)
        {
            mover.SetPosition(s.Position);
            return;
        }

        // cross-space: require coords/layer
        if (_coords is null || _sceneLayer is null)
            throw new InvalidOperationException("Cross-space conversion requires coordinates/layer.");

        if (s.MovementSpace == CoordinateSpace.Grid && mover.PositionSpace == CoordinateSpace.Pixel)
        {
            var px = _coords.GetAnchorPixelAtSceneLayerCoordinates(_sceneLayer,
                      new PointF(s.Position.X, s.Position.Y));
            mover.SetPosition(new Vector2(px.X, px.Y));
            return;
        }

        if (s.MovementSpace == CoordinateSpace.Pixel && mover.PositionSpace == CoordinateSpace.Grid)
        {
            // prefer PointF-taking API if you add one; avoid early truncation
            var gp = _coords.GetSceneLayerCoordinatesAtPixel(_sceneLayer,
                      new Point((int)s.Position.X, (int)s.Position.Y));
            mover.SetPosition(new Vector2(gp.X, gp.Y));
            return;
        }

        // should not reach here...
        throw new InvalidOperationException($"Unsupported conversion {s.MovementSpace}->{mover.PositionSpace}");
    }

    /// <summary>
    /// Limits the current velocity to the maximum speed, if specified.
    /// </summary>
    /// <remarks>If <see cref="MaxSpeed"/> is not set, the method does nothing. If the current velocity
    /// exceeds the  maximum speed, it is scaled down proportionally to ensure its magnitude does not exceed the
    /// maximum.</remarks>
    private static void ClampVelocity(ref MovementState movementState)
    {
        if (movementState.MaxSpeed is null)
            return;

        var v = movementState.Velocity;
        var speed = v.Length();
        var max = movementState.MaxSpeed.Value;

        if (max > 0 && speed > max)
            movementState.Velocity = v * (max / speed);
    }
}
