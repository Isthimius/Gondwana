using Gondwana.Physics.Movement.Easing;
using Gondwana.Physics.Movement.Scripted;
using System.Numerics;

namespace Gondwana.Physics.Movement;

public sealed partial class MovementController
{
    private Action<ScriptedMovement>? _scriptCompleted;

    /// <summary>
    /// Invokes a callback for the currently active scripted movement.
    /// </summary>
    /// <remarks>
    /// Scripted movements begin immediately when they are created, so callbacks
    /// registered through this fluent method are invoked immediately.
    /// </remarks>
    /// <param name="callback">The callback to invoke.</param>
    /// <returns>
    /// The current movement controller for fluent configuration.
    /// </returns>
    public MovementController OnBeginning(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        return OnBeginning(_ => callback());
    }

    /// <summary>
    /// Invokes a callback for the currently active scripted movement.
    /// </summary>
    /// <remarks>
    /// Scripted movements begin immediately when they are created, so callbacks
    /// registered through this fluent method are invoked immediately.
    /// </remarks>
    /// <param name="callback">
    /// The callback to invoke with the active scripted movement.
    /// </param>
    /// <returns>
    /// The current movement controller for fluent configuration.
    /// </returns>
    public MovementController OnBeginning(
        Action<ScriptedMovement> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (!IsScripted)
        {
            throw new InvalidOperationException(
                "OnBeginning must be called while a scripted movement is active.");
        }

        callback(_state.Script);

        return this;
    }

    /// <summary>
    /// Registers a callback to invoke when the currently active scripted movement
    /// completes normally.
    /// </summary>
    /// <remarks>
    /// The callback is not invoked if the scripted movement is cancelled
    /// or replaced.
    /// </remarks>
    /// <param name="callback">The callback to invoke.</param>
    /// <returns>
    /// The current movement controller for fluent configuration.
    /// </returns>
    public MovementController OnComplete(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        return OnComplete(_ => callback());
    }

    /// <summary>
    /// Registers a callback to invoke when the currently active scripted movement
    /// completes normally.
    /// </summary>
    /// <remarks>
    /// The callback is not invoked if the scripted movement is cancelled
    /// or replaced.
    /// </remarks>
    /// <param name="callback">
    /// The callback to invoke with the completed scripted movement.
    /// </param>
    /// <returns>
    /// The current movement controller for fluent configuration.
    /// </returns>
    public MovementController OnComplete(
        Action<ScriptedMovement> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (!IsScripted)
        {
            throw new InvalidOperationException(
                "OnComplete must be called while a scripted movement is active.");
        }

        _scriptCompleted += callback;

        return this;
    }

    /// <summary>
    /// Begins a scripted tween toward the specified
    /// <paramref name="target"/> position over a fixed duration.
    /// This method interpolates linearly or with an optional easing function
    /// from the mover's current position to the target, replacing any existing
    /// scripted movement and clearing physics-based movement.
    /// </summary>
    /// <param name="target">The absolute destination position.</param>
    /// <param name="durationSec">
    /// The total tween duration in seconds.
    /// Values less than 0 are clamped to 0.
    /// </param>
    /// <param name="easing">
    /// Optional easing function mapping normalized time [0,1] → [0,1].
    /// If null, linear easing is used.
    /// </param>
    /// <param name="snapEpsilon">
    /// The arrival tolerance. When the mover is within this distance of the target,
    /// the tween completes early. Values less than 0 are clamped to 0.
    /// </param>
    /// <returns>
    /// The current movement controller for fluent configuration.
    /// </returns>
    public MovementController MoveTo(
        Vector2 target,
        float durationSec,
        Func<float, float>? easing = null,
        float snapEpsilon = 0.5f)
    {
        return StartScript(new ScriptedMovement
        {
            Type = MovementScriptType.TweenTo,
            Origin = _mover.GetPosition(),
            Target = target,
            DurationSec = MathF.Max(0f, durationSec),
            ElapsedSec = 0f,
            SnapEpsilon = MathF.Max(0f, snapEpsilon),
            Easing = easing
        });
    }

    /// <summary>
    /// Begins a scripted tween toward the specified
    /// <paramref name="target"/> position over a fixed duration,
    /// using a predefined <see cref="EasingKind"/> curve.
    /// </summary>
    /// <param name="target">The absolute destination position.</param>
    /// <param name="seconds">
    /// The tween duration in seconds.
    /// Values less than 0 are clamped to 0.
    /// </param>
    /// <param name="easingKind">
    /// The built-in easing preset to apply.
    /// </param>
    /// <param name="snapEpsilon">
    /// The arrival tolerance. When the mover is within this distance of the target,
    /// the tween completes early.
    /// </param>
    /// <returns>
    /// The current movement controller for fluent configuration.
    /// </returns>
    public MovementController MoveTo(
        Vector2 target,
        float seconds,
        EasingKind easingKind,
        float snapEpsilon = 0.5f)
    {
        var easingFunc = EasingFunctions.From(easingKind);

        return MoveTo(
            target,
            seconds,
            easingFunc,
            snapEpsilon);
    }

    /// <summary>
    /// Begins scripted relative motion by a delta over a fixed duration
    /// with optional easing.
    /// Interprets <paramref name="delta"/> in the mover's position space.
    /// </summary>
    /// <param name="delta">
    /// The offset by which to move, relative to the mover's current position.
    /// Units are in the mover's position space.
    /// </param>
    /// <param name="durationSec">
    /// The total duration of the motion in seconds.
    /// </param>
    /// <param name="easing">
    /// Optional easing function that determines interpolation over time.
    /// If null, the motion is linear.
    /// </param>
    /// <param name="snapEpsilon">
    /// The distance threshold at which the motion snaps to the goal and completes.
    /// </param>
    /// <returns>
    /// The current movement controller for fluent configuration.
    /// </returns>
    public MovementController MoveBy(
        Vector2 delta,
        float durationSec,
        Func<float, float>? easing = null,
        float snapEpsilon = 0.5f)
    {
        var goal = _mover.GetPosition() + delta;

        return MoveTo(
            goal,
            durationSec,
            easing,
            snapEpsilon);
    }

    /// <summary>
    /// Begins scripted relative motion by a delta over a fixed duration
    /// using an easing preset.
    /// </summary>
    /// <param name="delta">
    /// The offset by which to move, relative to the mover's current position.
    /// Units are in the mover's position space.
    /// </param>
    /// <param name="durationSec">
    /// The total duration of the motion in seconds.
    /// </param>
    /// <param name="easingKind">
    /// The predefined easing curve to apply during the motion.
    /// </param>
    /// <param name="snapEpsilon">
    /// The distance threshold at which the motion snaps to the goal and completes.
    /// </param>
    /// <returns>
    /// The current movement controller for fluent configuration.
    /// </returns>
    public MovementController MoveBy(
        Vector2 delta,
        float durationSec,
        EasingKind easingKind,
        float snapEpsilon = 0.5f)
    {
        var goal = _mover.GetPosition() + delta;

        return MoveTo(
            goal,
            durationSec,
            easingKind,
            snapEpsilon);
    }

    /// <summary>
    /// Begins scripted relative motion by a delta at a constant speed.
    /// </summary>
    /// <param name="delta">
    /// The offset by which to move, relative to the mover's current position.
    /// Units are in the mover's position space.
    /// </param>
    /// <param name="speedPerSec">
    /// The movement speed in position-space units per second.
    /// </param>
    /// <param name="snapEpsilon">
    /// The distance threshold at which the motion snaps to the goal and completes.
    /// </param>
    /// <returns>
    /// The current movement controller for fluent configuration.
    /// </returns>
    public MovementController MoveBy(
        Vector2 delta,
        float speedPerSec,
        float snapEpsilon = 0.5f)
    {
        var goal = _mover.GetPosition() + delta;

        return MoveToward(
            goal,
            MathF.Max(0f, speedPerSec),
            snapEpsilon);
    }

    /// <summary>
    /// Begins scripted motion toward the specified target position
    /// at a constant speed.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="MoveTo(Vector2, float, Func{float, float}?, float)"/>,
    /// this motion continues each frame until the target is reached or cancelled,
    /// rather than running for a fixed duration.
    /// </remarks>
    /// <param name="target">The absolute destination position.</param>
    /// <param name="speedPerSec">
    /// The movement speed per second.
    /// Values less than 0 are clamped to 0.
    /// </param>
    /// <param name="snapEpsilon">
    /// The arrival tolerance. When the mover is within this distance of the target,
    /// motion completes early. Values less than 0 are clamped to 0.
    /// </param>
    /// <returns>
    /// The current movement controller for fluent configuration.
    /// </returns>
    public MovementController MoveToward(
        Vector2 target,
        float speedPerSec,
        float snapEpsilon = 0.5f)
    {
        return StartScript(new ScriptedMovement
        {
            Type = MovementScriptType.Toward,
            Target = target,
            SpeedPerSec = MathF.Max(0f, speedPerSec),
            SnapEpsilon = MathF.Max(0f, snapEpsilon)
        });
    }

    /// <summary>
    /// Cancels any active scripted movement and clears the current script state.
    /// Raises <see cref="ScriptedMovementStopped"/> if a script was active.
    /// Registered completion callbacks are discarded without being invoked.
    /// </summary>
    public void CancelScript()
    {
        if (!IsScripted)
        {
            _scriptCompleted = null;
            return;
        }

        var script = _state.Script;

        // Clear the old script before invoking external code so that an event
        // handler can safely begin a new movement.
        _state.Script = default;
        _scriptCompleted = null;

        ScriptedMovementStopped?.Invoke(script);
    }

    private MovementController StartScript(
        ScriptedMovement script)
    {
        // A newly assigned script supersedes any completion callback associated
        // with the previously active script.
        _scriptCompleted = null;

        _state.Script = script;

        // Scripted movement overrides integrated movement.
        _state.Acceleration = Vector2.Zero;
        _state.Velocity = Vector2.Zero;

        ScriptedMovementStarted?.Invoke(script);

        return this;
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

            _state.Acceleration = Vector2.Zero;
            _state.Velocity = Vector2.Zero;

            Step(0f);
            CompleteScript();

            return true;
        }

        _state.Script.ElapsedSec += MathF.Max(0f, dt);

        float t = Math.Clamp(
            _state.Script.ElapsedSec / _state.Script.DurationSec,
            0f,
            1f);

        if (_state.Script.Easing is not null)
        {
            t = Math.Clamp(
                _state.Script.Easing(t),
                0f,
                1f);
        }

        var pos = Vector2.Lerp(
            _state.Script.Origin,
            _state.Script.Target,
            t);

        _mover.SetPosition(pos);

        _state.Acceleration = Vector2.Zero;
        _state.Velocity = Vector2.Zero;

        Step(0f);

        var snapDistanceSquared =
            _state.Script.SnapEpsilon *
            _state.Script.SnapEpsilon;

        bool reachedTarget =
            Vector2.DistanceSquared(
                pos,
                _state.Script.Target) <= snapDistanceSquared;

        if (reachedTarget || t >= 1f)
        {
            _mover.SetPosition(_state.Script.Target);

            _state.Acceleration = Vector2.Zero;
            _state.Velocity = Vector2.Zero;

            Step(0f);
            CompleteScript();
        }

        return true;
    }

    private bool AdvanceToward(float dt)
    {
        var current = _mover.GetPosition();
        var to = _state.Script.Target - current;
        var distance = to.Length();

        if (distance <= _state.Script.SnapEpsilon ||
            _state.Script.SpeedPerSec <= 0f ||
            dt <= 0f)
        {
            _mover.SetPosition(_state.Script.Target);

            _state.Velocity = Vector2.Zero;
            _state.Acceleration = Vector2.Zero;

            Step(0f);
            CompleteScript();

            return true;
        }

        var stepLength =
            _state.Script.SpeedPerSec * dt;

        if (stepLength >= distance)
        {
            _mover.SetPosition(_state.Script.Target);

            _state.Velocity = Vector2.Zero;
            _state.Acceleration = Vector2.Zero;

            Step(0f);
            CompleteScript();

            return true;
        }

        var direction = to / distance;

        _state.Acceleration = Vector2.Zero;
        _state.Velocity =
            direction *
            _state.Script.SpeedPerSec;

        Step(dt);

        return true;
    }

    private void CompleteScript()
    {
        var script = _state.Script;
        var completed = _scriptCompleted;

        // Clear the completed movement before invoking external code.
        // Event handlers and callbacks may safely begin another movement
        // without that new movement subsequently being erased.
        _state.Script = default;
        _scriptCompleted = null;

        ScriptedMovementStopped?.Invoke(script);
        completed?.Invoke(script);
    }
}