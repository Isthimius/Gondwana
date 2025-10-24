using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Movement.Scripted;
using Gondwana.Scenes;

namespace Gondwana.Movement;

public sealed class MovementController
{
    private readonly ISceneLayerCoordinates? _coords;
    private readonly SceneLayer? _sceneLayer;

    private readonly IMovable _target;
    private MovementState _state;
     
    // Pixel-only or Grid-only same-space usage
    private MovementController() { }

    // Layer-aware usage (Grid↔Pixel conversion, wrapping)
    private MovementController(SceneLayer sceneLayer)
    {
        if (sceneLayer is null)
            throw new ArgumentNullException(nameof(sceneLayer));

        if (sceneLayer.CoordinateSystem is null)
            throw new ArgumentException("SceneLayer must have a CoordinateSystem.", nameof(sceneLayer));

        _sceneLayer  = sceneLayer;
        _coords = _sceneLayer.CoordinateSystem;
    }

    /// <summary>
    /// handle physics-based (velocity/acceleration, damping) movement step over dt seconds
    /// </summary>
    internal void Step(IMovable mover, ref MovementState s, float dt)
    {
        // integrate
        s.Velocity += s.Acceleration * dt;
        ClampVelocity(ref s);
        
        if (s.LinearDamping > 0f)
            s.Velocity *= MathF.Exp(-s.LinearDamping * dt);  // exp damping

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
    /// Move <paramref name="mover"/> toward <paramref name="target"/> over <paramref name="durationSec"/> seconds,
    /// using optional easing. Caller supplies and owns <paramref name="elapsedSec"/> (accumulate dt each frame).
    /// Returns true when snapped to target.
    /// </summary>
    /// <remarks>
    /// The <paramref name="easing"/> parameter allows you to customize how interpolation progresses over time.
    /// It accepts a delegate of type <see cref="Func{T,TResult}"/> that takes a normalized time value (0..1)
    /// and returns a modified value (also 0..1) that controls the easing curve.
    ///
    /// Common examples:
    ///
    /// <code>
    /// // Linear (no easing)
    /// float Linear(float t) => t;
    ///
    /// // Ease-in (slow start)
    /// float EaseInQuad(float t) => t * t;
    ///
    /// // Ease-out (slow end)
    /// float EaseOutQuad(float t) => 1 - (1 - t) * (1 - t);
    ///
    /// // Ease-in-out (slow start and end)
    /// float EaseInOutQuad(float t)
    /// {
    ///     return t < 0.5f
    ///         ? 2f * t * t
    ///         : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f;
    /// }
    ///
    /// // Example usage:
    /// float elapsed = 0f;
    /// while (!controller.MoveTo(mover, ref state, target, duration, ref elapsed, EaseInOutQuad))
    /// {
    ///     elapsed += dt;
    ///     ...
    /// }
    /// </code>
    ///
    /// If <paramref name="easing"/> is null, interpolation defaults to linear.
    /// </remarks>
    internal bool MoveTo(
        IMovable mover,
        ref MovementState s,
        Vector2 target,
        float durationSec,
        ref float elapsedSec,
        Func<float, float>? easing = null,
        float snapEpsilon = 0.01f)
    {
        if (durationSec <= 0f)
        {
            // instant snap
            s.Acceleration = Vector2.Zero;
            s.Velocity = Vector2.Zero;
            s.Position = target;
            // apply directly (no integration)
            Step(mover, ref s, 0f);
            return true;
        }

        float t = Math.Clamp(elapsedSec / durationSec, 0f, 1f);
        if (easing is not null) t = Math.Clamp(easing(t), 0f, 1f);

        // Lerp in the state's own space (Grid or Pixel)
        var pos = Vector2.Lerp(s.Position, target, t); // using current pos -> avoids drift if interrupted
        s.Acceleration = Vector2.Zero;
        s.Velocity = Vector2.Zero;
        s.Position = pos;

        // apply without advancing time
        Step(mover, ref s, 0f);

        // snap when close enough
        if (Vector2.DistanceSquared(pos, target) <= snapEpsilon * snapEpsilon || t >= 1f)
        {
            s.Position = target;
            Step(mover, ref s, 0f);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Advance one frame toward <paramref name="target"/> at <paramref name="speedPerSec"/> (in s.MovementSpace units).
    /// Call each frame with dt via Step(...). Returns true when snapped to target.
    /// </summary>
    internal bool MoveToward(
        IMovable mover,
        ref MovementState s,
        Vector2 target,
        float speedPerSec,
        float dt,
        float snapEpsilon = 0.01f)
    {
        var to = target - s.Position;
        var dist = to.Length();
        if (dist <= snapEpsilon || speedPerSec <= 0f || dt <= 0f)
        {
            s.Position = target;
            s.Velocity = Vector2.Zero;
            s.Acceleration = Vector2.Zero;
            Step(mover, ref s, 0f);
            return true;
        }

        var step = speedPerSec * dt;
        if (step >= dist)
        {
            // we can reach this frame
            s.Position = target;
            s.Velocity = Vector2.Zero;
            s.Acceleration = Vector2.Zero;
            Step(mover, ref s, 0f);
            return true;
        }

        // advance toward
        var dir = to / dist;
        s.Acceleration = Vector2.Zero;
        s.Velocity = dir * speedPerSec;
        Step(mover, ref s, dt);
        return false;
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

    #region scripted movement

    // --- Scheduling API (called by game code / DirectDrawingMovableBase) ---
    internal void ScheduleMoveTo(ref MovementState s, Vector2 target, float durationSec,
                                 Func<float, float>? easing = null, float snapEpsilon = 0.5f)
    {
        s.Script = new ScriptedMovement
        {
            Type = MovementScriptType.TweenTo,
            Target = target,
            DurationSec = MathF.Max(0f, durationSec),
            ElapsedSec = 0f,
            SnapEpsilon = MathF.Max(0f, snapEpsilon),
            Easing = easing
        };

        // scripted motion overrides physics; zero them
        s.Acceleration = Vector2.Zero;
        s.Velocity = Vector2.Zero;
    }

    internal void ScheduleMoveToward(ref MovementState s, Vector2 target, float speedPerSec,
                                     float snapEpsilon = 0.5f)
    {
        s.Script = new ScriptedMovement
        {
            Type = MovementScriptType.Toward,
            Target = target,
            SpeedPerSec = MathF.Max(0f, speedPerSec),
            SnapEpsilon = MathF.Max(0f, snapEpsilon)
        };
        s.Acceleration = Vector2.Zero;
        s.Velocity = Vector2.Zero;
    }

    internal void CancelScript(ref MovementState s)
    {
        s.Script = default;
    }

    // --- One place to advance any scripted motion; returns true if it handled movement this frame ---
    internal bool AdvanceScripted(IMovable mover, ref MovementState s, float dt)
    {
        switch (s.Script.Type)
        {
            case MovementScriptType.None:
                return false;

            case MovementScriptType.TweenTo:
                return AdvanceTween(mover, ref s, dt);

            case MovementScriptType.Toward:
                return AdvanceToward(mover, ref s, dt);

            default:
                return false;
        }
    }

    // Unified private helpers (lifted from your existing code paths) :contentReference[oaicite:9]{index=9}
    private bool AdvanceTween(IMovable mover, ref MovementState s, float dt)
    {
        if (s.Script.DurationSec <= 0f)
        {
            s.Position = s.Script.Target;
            Step(mover, ref s, 0f);
            s.Script = default;

            return true;
        }

        s.Script.ElapsedSec += MathF.Max(0f, dt);
        float t = Math.Clamp(s.Script.ElapsedSec / s.Script.DurationSec, 0f, 1f);
        if (s.Script.Easing is not null)
            t = Math.Clamp(s.Script.Easing(t), 0f, 1f);

        var pos = Vector2.Lerp(s.Position, s.Script.Target, t);
        s.Position = pos;
        s.Acceleration = Vector2.Zero;
        s.Velocity = Vector2.Zero;
        Step(mover, ref s, 0f);

        if (Vector2.DistanceSquared(pos, s.Script.Target) <= s.Script.SnapEpsilon * s.Script.SnapEpsilon || t >= 1f)
        {
            s.Position = s.Script.Target;
            Step(mover, ref s, 0f);
            s.Script = default;

            if (mover is IScriptedMovementListener listener)
                listener.OnScriptedMovementStopped();
        }

        return true;
    }

    private bool AdvanceToward(IMovable mover, ref MovementState s, float dt)
    {
        var to = s.Script.Target - s.Position;
        var dist = to.Length();

        if (dist <= s.Script.SnapEpsilon || s.Script.SpeedPerSec <= 0f || dt <= 0f)
        {
            s.Position = s.Script.Target;
            s.Velocity = Vector2.Zero;
            s.Acceleration = Vector2.Zero;
            Step(mover, ref s, 0f);
            s.Script = default;

            if (mover is IScriptedMovementListener listener)
                listener.OnScriptedMovementStopped();

            return true;
        }

        var stepLen = s.Script.SpeedPerSec * dt;
        if (stepLen >= dist)
        {
            s.Position = s.Script.Target;
            s.Velocity = Vector2.Zero;
            s.Acceleration = Vector2.Zero;
            Step(mover, ref s, 0f);
            s.Script = default;

            return true;
        }

        var dir = to / dist;
        s.Acceleration = Vector2.Zero;
        s.Velocity = dir * s.Script.SpeedPerSec;
        Step(mover, ref s, dt);

        return true;
    }

    #endregion scripted movement

    #region static factories

    internal static MovementController ForPixelOverlay() => new();
    internal static MovementController ForSceneLayer(SceneLayer layer) => new(layer);

    #endregion static factories
}
