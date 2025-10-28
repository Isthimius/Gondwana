using Gondwana.Movement.Easing;
using Gondwana.Movement.Scripted;
using System.Numerics;

namespace Gondwana.Movement;

public sealed partial class MovementController
{
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
    }

    public void MoveTo(Vector2 target, float seconds, EasingKind easingKind, float snapEps = 0.5f)
    {
        var easingFunc = EasingFunctions.From(easingKind);
        MoveTo(target, seconds, easingFunc, snapEps);
    }

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
    }

    public void CancelScript()
    {
        if (IsScripted)
            ScriptedMovementStopped?.Invoke();

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
            _state.Script = default;

            ScriptedMovementStopped?.Invoke();
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
            _state.Script = default;

            ScriptedMovementStopped?.Invoke();
            return true;
        }

        var stepLen = _state.Script.SpeedPerSec * dt;
        if (stepLen >= dist)
        {
            _mover.SetPosition(_state.Script.Target);
            _state.Velocity = Vector2.Zero;
            _state.Acceleration = Vector2.Zero;
            Step(0f);
            _state.Script = default;

            ScriptedMovementStopped?.Invoke();
            return true;
        }

        var dir = to / dist;
        _state.Acceleration = Vector2.Zero;
        _state.Velocity = dir * _state.Script.SpeedPerSec;
        Step(dt);

        return true;
    }
}
