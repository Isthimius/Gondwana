using Gondwana.Physics.Movement.Scripted;
using Gondwana.Physics.Movement.Easing;
using System.Numerics;

namespace Gondwana.Physics.Movement;

public sealed partial class MovementController
{
    /// <summary>
    /// Begins a scripted tween toward the specified <paramref name="target"/> position over a fixed duration.
    /// This method interpolates linearly or with an optional easing function from the mover’s current position
    /// to the target, automatically cancelling any existing scripted or physics-based movement.
    /// </summary>
    /// <param name="target">The absolute destination position.</param>
    /// <param name="durationSec">The total tween duration in seconds. Values less than 0 are clamped to 0.</param>
    /// <param name="easing">Optional easing function mapping normalized time [0,1] → [0,1]. If null, linear easing is used.</param>
    /// <param name="snapEpsilon">
    /// The arrival tolerance. When the mover is within this distance of the target, the tween completes early.
    /// Values less than 0 are clamped to 0.
    /// </param>
    public void MoveTo(Vector2 target, float durationSec, Func<float, float>? easing = null, float snapEpsilon = 0.5f)
    {
        _state.Script = new ScriptedMovement
        {
            Type = MovementScriptType.TweenTo,
            Origin = _mover.GetPosition(),
            Target = target,
            DurationSec = MathF.Max(0f, durationSec),
            ElapsedSec = 0f,
            SnapEpsilon = MathF.Max(0f, snapEpsilon),
            Easing = easing
        };

        // scripted motion overrides physics; zero them
        _state.Acceleration = Vector2.Zero;
        _state.Velocity = Vector2.Zero;

        ScriptedMovementStarted?.Invoke(_state.Script);
    }

    /// <summary>
    /// Begins a scripted tween toward the specified <paramref name="target"/> position over a fixed duration,
    /// using a predefined <see cref="EasingKind"/> curve. This overload is a convenience wrapper around
    /// <see cref="MoveTo(Vector2, float, Func{float, float}?, float)"/>.
    /// </summary>
    /// <param name="target">The absolute destination position.</param>
    /// <param name="seconds">The tween duration in seconds. Values less than 0 are clamped to 0.</param>
    /// <param name="easingKind">The built-in easing preset to apply.</param>
    /// <param name="snapEpsilon">
    /// The arrival tolerance. When the mover is within this distance of the target, the tween completes early.
    /// </param>
    public void MoveTo(Vector2 target, float seconds, EasingKind easingKind, float snapEpsilon = 0.5f)
    {
        var easingFunc = EasingFunctions.From(easingKind);
        MoveTo(target, seconds, easingFunc, snapEpsilon);
    }

    /// <summary>
    /// Scripted relative motion by a delta over a fixed duration (with optional easing).
    /// Interprets <paramref name="delta"/> in the mover's PositionSpace (Grid or Pixel).
    /// </summary>
    /// <param name="delta">
    /// The offset by which to move, relative to the mover’s current position.
    /// Units are in the mover’s PositionSpace (grid cells or pixels).
    /// </param>
    /// <param name="durationSec">
    /// The total duration of the motion in seconds.
    /// </param>
    /// <param name="easing">
    /// Optional easing function that determines interpolation over time.
    /// If null, the motion is linear.
    /// </param>
    /// <param name="snapEpsilon">
    /// The distance threshold (in PositionSpace units) at which the motion will snap to the goal and stop.
    /// </param>
    public void MoveBy(Vector2 delta, float durationSec, Func<float, float>? easing = null, float snapEpsilon = 0.5f)
    {
        var goal = _mover.GetPosition() + delta;
        MoveTo(goal, durationSec, easing, snapEpsilon);   // delegate to existing tween
    }

    /// <summary>
    /// Scripted relative motion by a delta over a fixed duration using an easing preset.
    /// </summary>
    /// <param name="delta">
    /// The offset by which to move, relative to the mover’s current position.
    /// Units are in the mover’s PositionSpace (grid cells or pixels).
    /// </param>
    /// <param name="durationSec">
    /// The total duration of the motion in seconds.
    /// </param>
    /// <param name="easingKind">
    /// The predefined easing curve (e.g., EaseInOutQuad, EaseOutCubic) to apply during the motion.
    /// </param>
    /// <param name="snapEpsilon">
    /// The distance threshold (in PositionSpace units) at which the motion will snap to the goal and stop.
    /// </param>
    public void MoveBy(Vector2 delta, float durationSec, EasingKind easingKind, float snapEpsilon = 0.5f)
    {
        var goal = _mover.GetPosition() + delta;
        MoveTo(goal, durationSec, easingKind, snapEpsilon); // overload
    }

    /// <summary>
    /// Scripted relative motion by a delta at a constant speed (units/sec in the mover's PositionSpace).
    /// </summary>
    /// <param name="delta">
    /// The offset by which to move, relative to the mover’s current position.
    /// Units are in the mover’s PositionSpace (grid cells or pixels).
    /// </param>
    /// <param name="speedPerSec">
    /// The speed of movement in PositionSpace units per second (e.g., pixels/sec or tiles/sec).
    /// </param>
    /// <param name="snapEpsilon">
    /// The distance threshold (in PositionSpace units) at which the motion will snap to the goal and stop.
    /// </param>
    public void MoveBy(Vector2 delta, float speedPerSec, float snapEpsilon = 0.5f)
    {
        var goal = _mover.GetPosition() + delta;
        MoveToward(goal, MathF.Max(0f, speedPerSec), snapEpsilon); // delegate to Toward
    }

    /// <summary>
    /// Begins a scripted motion toward the <paramref name="target"/> position at a constant speed.
    /// Unlike <see cref="MoveTo(Vector2, float, Func{float, float}?, float)"/>, this motion continues each frame
    /// until the target is reached or cancelled, rather than running for a fixed duration.
    /// </summary>
    /// <param name="target">The absolute destination position.</param>
    /// <param name="speedPerSec">The movement speed per second. Values less than 0 are clamped to 0.</param>
    /// <param name="snapEpsilon">
    /// The arrival tolerance. When the mover is within this distance of the target, motion completes early.
    /// Values less than 0 are clamped to 0.
    /// </param>
    public void MoveToward(Vector2 target, float speedPerSec, float snapEpsilon = 0.5f)
    {
        _state.Script = new ScriptedMovement
        {
            Type = MovementScriptType.Toward,
            Target = target,
            SpeedPerSec = MathF.Max(0f, speedPerSec),
            SnapEpsilon = MathF.Max(0f, snapEpsilon)
        };

        _state.Acceleration = Vector2.Zero;
        _state.Velocity = Vector2.Zero;

        ScriptedMovementStarted?.Invoke(_state.Script);
    }

    /// <summary>
    /// Cancels any active scripted movement (tween or constant-speed) and clears the current script state.
    /// Raises <see cref="ScriptedMovementStopped"/> if a script was active.
    /// </summary>
    public void CancelScript()
    {
        if (IsScripted)
            ScriptedMovementStopped?.Invoke(_state.Script);

        _state.Script = default;
    }

    private bool AdvanceScripted(float dt)
    {
        switch (_state.Script.Type)
        {
            case MovementScriptType.None:
                return false;

            case MovementScriptType.TweenTo:
                return AdvanceTween(dt);

            case MovementScriptType.Toward:
                return AdvanceToward(dt);

            default:
                return false;
        }
    }

    private bool AdvanceTween(float dt)
    {
        if (_state.Script.DurationSec <= 0f)
        {
            _mover.SetPosition(_state.Script.Target);
            Step(0f);
            _state.Script = default;

            return true;
        }

        _state.Script.ElapsedSec += MathF.Max(0f, dt);
        float t = Math.Clamp(_state.Script.ElapsedSec / _state.Script.DurationSec, 0f, 1f);

        if (_state.Script.Easing is not null)
            t = Math.Clamp(_state.Script.Easing(t), 0f, 1f);

        var pos = Vector2.Lerp(_state.Script.Origin, _state.Script.Target, t);
        _mover.SetPosition(pos);
        _state.Acceleration = Vector2.Zero;
        _state.Velocity = Vector2.Zero;
        Step(0f);

        if (Vector2.DistanceSquared(pos, _state.Script.Target) <= _state.Script.SnapEpsilon * _state.Script.SnapEpsilon || t >= 1f)
        {
            _mover.SetPosition(_state.Script.Target);
            Step(0f);

            ScriptedMovementStopped?.Invoke(_state.Script);
            _state.Script = default;
        }

        return true;
    }

    private bool AdvanceToward(float dt)
    {
        var current = _mover.GetPosition();
        var to = _state.Script.Target - current;
        var dist = to.Length();

        if (dist <= _state.Script.SnapEpsilon || _state.Script.SpeedPerSec <= 0f || dt <= 0f)
        {
            _mover.SetPosition(_state.Script.Target);
            _state.Velocity = Vector2.Zero;
            _state.Acceleration = Vector2.Zero;
            Step(0f);

            ScriptedMovementStopped?.Invoke(_state.Script);
            _state.Script = default;

            return true;
        }

        var stepLen = _state.Script.SpeedPerSec * dt;
        if (stepLen >= dist)
        {
            _mover.SetPosition(_state.Script.Target);
            _state.Velocity = Vector2.Zero;
            _state.Acceleration = Vector2.Zero;
            Step(0f);

            ScriptedMovementStopped?.Invoke(_state.Script);
            _state.Script = default;

            return true;
        }

        var dir = to / dist;
        _state.Acceleration = Vector2.Zero;
        _state.Velocity = dir * _state.Script.SpeedPerSec;
        Step(dt);

        return true;
    }
}
