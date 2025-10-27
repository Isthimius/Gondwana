using System.Numerics;
using Gondwana.Movement.Scripted;

namespace Gondwana.Movement;

public sealed partial class MovementController
{
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

        var pos = Vector2.Lerp(s.Script.Origin, s.Script.Target, t);
        s.Position = pos;
        s.Acceleration = Vector2.Zero;
        s.Velocity = Vector2.Zero;
        Step(mover, ref s, 0f);

        if (Vector2.DistanceSquared(pos, s.Script.Target) <= s.Script.SnapEpsilon * s.Script.SnapEpsilon || t >= 1f)
        {
            s.Position = s.Script.Target;
            Step(mover, ref s, 0f);
            s.Script = default;

            ScriptedMovementStopped?.Invoke();
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

            ScriptedMovementStopped?.Invoke();
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

            ScriptedMovementStopped?.Invoke();
            return true;
        }

        var dir = to / dist;
        s.Acceleration = Vector2.Zero;
        s.Velocity = dir * s.Script.SpeedPerSec;
        Step(mover, ref s, dt);

        return true;
    }

    private void ScheduleMoveTo(ref MovementState s, Vector2 target, float durationSec,
                                 Func<float, float>? easing = null, float snapEpsilon = 0.5f)
    {
        s.Script = new ScriptedMovement
        {
            Type = MovementScriptType.TweenTo,
            Origin = s.Position,
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
}
