using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Movement.Scripted;
using Gondwana.Scenes;

namespace Gondwana.Movement;

public sealed class MovementController : IDisposable
{
    private readonly ISceneLayerCoordinates? _coords;
    private readonly SceneLayer? _sceneLayer;
    private readonly IMovable _target;
    private MovementState _state;

    // --- Pixel-follow (DirectDrawing → DirectDrawing)
    private Func<Vector2>? _followPixel;   // returns current target pixel position each frame
    private Vector2 _followOffsetPx;
    private bool _followHard;
    private float _followSpeedPxPerSec;
    private float _followSnapPx;

    // Grid-follow (Scene-layer → this DirectDrawing in pixels)
    private IMovableOnSceneLayer? _followGridTarget;
    private Vector2 _followGridOffset;
    private float _followSpeedTilesPerSec;
    private float _followSnapTiles;

    public event Action? ScriptedMovementStopped;

    // Bind to one target + initial state. Optional layer when you need Grid↔Pixel & wrapping.
    internal MovementController(IMovable target, MovementState initial, SceneLayer? layer = null)
    {
        _target = target;
        _state = initial;

        if (layer is not null)
        {
            _sceneLayer = layer;
            _coords = layer.CoordinateSystem ?? throw new ArgumentException("SceneLayer must have a CoordinateSystem.", nameof(layer));
        }
    }

    public MovementState MovementState => _state;               // snapshot
    public CoordinateSpace PositionSpace => _target.PositionSpace;
    public Vector2 GetPosition() => _target.GetPosition();
    public void SetPosition(Vector2 p) { _state.Position = p; _target.SetPosition(p); }

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

    // Ergonomic scripted APIs (delegate to existing schedulers)
    public void MoveTo(Vector2 target, float seconds, Func<float, float>? easing = null, float snapEps = 0.5f)
        => ScheduleMoveTo(ref _state, target, seconds, easing, snapEps);

    public void MoveToward(Vector2 target, float speedPerSec, float snapEps = 0.5f)
        => ScheduleMoveToward(ref _state, target, speedPerSec, snapEps);

    public void StopScript() => CancelScript(ref _state);

    public bool AdvanceScripted(float dt)
    {
        // --- Pixel-follow ---
        if (_followPixel is not null)
        {
            var goal = _followPixel() + _followOffsetPx;
            if (_followHard) { SetPosition(goal); return true; }
            ScheduleMoveToward(ref _state, goal, _followSpeedPxPerSec, _followSnapPx);
            // fall through into scripted advance
        }

        // --- Grid-follow (convert grid→pixel each frame) ---
        if (_followGridTarget is not null)
        {
            var layer = _followGridTarget.SceneLayer;
            var coords = layer.CoordinateSystem
                        ?? throw new InvalidOperationException("Follow target layer has no CoordinateSystem.");

            var grid = _followGridTarget.GetPosition() + _followGridOffset;

            var pxNow = coords.GetAnchorPixelAtSceneLayerCoordinates(layer,
                         new System.Drawing.PointF(grid.X, grid.Y));
            var goalPx = new Vector2(pxNow.X, pxNow.Y);

            if (_followHard)
            {
                SetPosition(goalPx);
                return true;
            }
            else
            {
                // tiles/sec → px/sec (sample +X; good first-order approximation)
                var pxRight = coords.GetAnchorPixelAtSceneLayerCoordinates(layer,
                              new System.Drawing.PointF(grid.X + 1f, grid.Y));
                float pxPerTile = MathF.Max(1f, new Vector2(pxRight.X - pxNow.X, pxRight.Y - pxNow.Y).Length());

                float speedPxPerSec = _followSpeedTilesPerSec * pxPerTile;
                float snapPx = _followSnapTiles * pxPerTile;

                ScheduleMoveToward(ref _state, goalPx, speedPxPerSec, snapPx);
            }
            // fall through into scripted advance
        }

        // --- Your existing internal script engine (uses MovementScriptType) ---
        return AdvanceScripted(_target, ref _state, dt);
    }

    public void Step(float dt) { if (_state.HasMotion) Step(_target, ref _state, dt); }  // integrates & applies via IMovable

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

            return true;
        }

        var dir = to / dist;
        s.Acceleration = Vector2.Zero;
        s.Velocity = dir * s.Script.SpeedPerSec;
        Step(mover, ref s, dt);

        return true;
    }

    #endregion scripted movement

    public void SetVelocity(Vector2 v) { CancelScript(ref _state); _state.Velocity = v; }             // stop script on manual control
    public void SetAcceleration(Vector2 a) { CancelScript(ref _state); _state.Acceleration = a; }
    public void StopMovement() { CancelScript(ref _state); _state.Velocity = Vector2.Zero; _state.Acceleration = Vector2.Zero; }
    public void SetMaxSpeed(float? maxSpeed) { _state.MaxSpeed = maxSpeed; }
    public void SetLinearDamping(float dampingPerSec) { _state.LinearDamping = MathF.Max(0f, dampingPerSec); }

    public void SetWrapX(bool enabled) => _state.WrapX = enabled;
    public void SetWrapY(bool enabled) => _state.WrapY = enabled;

    #region follow methods

    // ---------------- Pixel-follow (convenience): pass a delegate for current pixel pos
    public void FollowHard(Func<Vector2> getPixelPos, Vector2 offsetPx = default)
    {
        _followPixel = getPixelPos ?? throw new ArgumentNullException(nameof(getPixelPos));
        _followOffsetPx = offsetPx;
        _followHard = true;
        _followGridTarget = null; // ensure only one mode is active
    }

    public void FollowSoft(Func<Vector2> getPixelPos, float speedPxPerSec,
                           float snapPx = 0.5f, Vector2 offsetPx = default)
    {
        if (speedPxPerSec <= 0) throw new ArgumentOutOfRangeException(nameof(speedPxPerSec));
        _followPixel = getPixelPos ?? throw new ArgumentNullException(nameof(getPixelPos));
        _followOffsetPx = offsetPx;
        _followHard = false;
        _followSpeedPxPerSec = speedPxPerSec;
        _followSnapPx = MathF.Max(0f, snapPx);
        _followGridTarget = null;
    }

    // ---------------- Grid-follow (for scene targets)
    public void FollowHard(IMovableOnSceneLayer gridTarget, Vector2 gridOffset = default)
    {
        _followGridTarget = gridTarget ?? throw new ArgumentNullException(nameof(gridTarget));
        _followGridOffset = gridOffset;
        _followHard = true;
        _followPixel = null;
    }

    public void FollowSoft(IMovableOnSceneLayer gridTarget, float speedTilesPerSec,
                           float snapTiles = 0.25f, Vector2 gridOffset = default)
    {
        if (speedTilesPerSec <= 0) throw new ArgumentOutOfRangeException(nameof(speedTilesPerSec));
        _followGridTarget = gridTarget ?? throw new ArgumentNullException(nameof(gridTarget));
        _followGridOffset = gridOffset;
        _followHard = false;
        _followSpeedTilesPerSec = speedTilesPerSec;
        _followSnapTiles = MathF.Max(0f, snapTiles);
        _followPixel = null;
    }

    public void Unfollow()
    {
        _followPixel = null;
        _followGridTarget = null;
        CancelScript(ref _state);
    }

    #endregion follow methods

    public void Dispose()
    {
        ScriptedMovementStopped = null;
    }
}
