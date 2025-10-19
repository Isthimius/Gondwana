using System.Drawing;
using System.Numerics;
using Gondwana.Movement;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Gondwana.Timers;

namespace Gondwana.Drawing.Direct;

public abstract class DirectDrawingMovableBase : DirectDrawingBase, IMovable, IMovementStateMutator, IScriptedMovementListener
{
    // --- Motion fields (pixel space) ---
    private MovementController _controller;
    private MovementState movementState;

    public event EventHandler? ScriptedMovementStopped;

    /// <summary>
    /// Called when a scripted movement (MoveTo/MoveToward) finishes or is cancelled.
    /// Override to add custom behavior.
    /// </summary>
    protected virtual void OnScriptedMovementStopped()
    {
        ScriptedMovementStopped?.Invoke(this, EventArgs.Empty);
    }

    void IScriptedMovementListener.OnScriptedMovementStopped() => OnScriptedMovementStopped();

    protected DirectDrawingMovableBase(RenderSurfaceHostBase renderSurfaceHost, Rectangle bounds)
        : base(renderSurfaceHost, bounds)
    {
        _controller = DirectDrawingManager.Instance.MovementController;
        // initialize motion at current position (px)
        movementState = MovementState.ForPixel(new Vector2(bounds.X, bounds.Y));
    }

    #region sprite / camera follow support

    // --- continuous follow state ---
    private IMovable? _followTarget;
    private SceneLayer? _followLayer;                    // inferred once from target
    private Vector2 _followOffset;                       // grid-space offset
    private bool _followHard;                            // true=hard, false=soft
    private float _followSpeedTilesPerSec;               // soft
    private float _followSnapEps = 0.25f;                // soft

    /// <summary>
    /// Gets the object this drawable is currently bound to for continuous follow,
    /// or <see langword="null"/> if it is not following anything.
    /// </summary>
    public IMovable? FollowTarget => _followTarget;

    /// <summary>
    /// Hard, continuous follow. Snaps every frame to the target's current grid position (+offset).
    /// One call sets it up; no per-frame calls needed.
    /// </summary>
    public void FollowHard(IMovableOnSceneLayer target, Vector2 gridOffset = default)
    {
        EnsureFollowBinding(target, gridOffset);
        _followHard = true;
    }

    /// <summary>
    /// Soft, continuous follow. Re-schedules a MoveToward each frame toward the target's current grid position (+offset).
    /// </summary>
    public void FollowSoft(IMovableOnSceneLayer target, float speedTilesPerSec, float snapEpsilon = 0.25f, Vector2 gridOffset = default)
    {
        EnsureFollowBinding(target, gridOffset);
        _followHard = false;
        _followSpeedTilesPerSec = MathF.Max(0f, speedTilesPerSec);
        _followSnapEps = MathF.Max(0f, snapEpsilon);
    }

    /// <summary>Stop continuous follow and any scripted motion in progress.</summary>
    public void Unfollow()
    {
        _followTarget = null;
        _followLayer = null;
        _controller.CancelScript(ref movementState);
    }

    // --- glue: infer layer and initialize a grid-state controller once ---
    private void EnsureFollowBinding(IMovableOnSceneLayer target, Vector2 gridOffset)
    {
        _followTarget = target;
        _followLayer = target.SceneLayer;
        _followOffset = gridOffset;

        _controller = MovementController.ForSceneLayer(_followLayer);
        var startGrid = target.GetPosition() + _followOffset;
        movementState = MovementState.ForSceneLayer(startGrid);
        _controller.Step(this, ref movementState, 0f);
    }

    #endregion sprite / camera follow support

    /// <summary>
    /// Gets a copy of the current <see cref="MovementState"/> representing
    /// this object's motion parameters and position. Changes made to the
    /// returned struct do not affect the internal state.
    /// Use <see cref="IMovementStateMutator"/> methods to modify motion.
    /// </summary>
    public MovementState MovementState => movementState;

    public CoordinateSpace PositionSpace => CoordinateSpace.Pixel;

    // MovementState is authoritative for motion calculations.
    // Expose the movement state's position (float precision) instead of truncating to Bounds.
    public Vector2 GetPosition() => movementState.Position;

    public void SetPosition(Vector2 p)
    {
        // Update display bounds from the (pixel) position. Round to reduce jitter instead of truncating.
        ForceRefresh();

        // Keep MovementState in sync with explicit SetPosition calls.
        movementState.Position = p;

        Bounds = new Rectangle((int)Math.Round(p.X), (int)Math.Round(p.Y), Bounds.Width, Bounds.Height);
        ForceRefresh();
    }

    protected internal override void Update(long tick)
    {
        if (tick == _lastTick)
            return;

        float dt = Gondwana.Timers.HighResTimer.GetDuration(_lastTick, tick);

        // --- Continuous follow path (runs first) ---
        if (_followTarget is not null && _followLayer is not null)
        {
            var targetGrid = _followTarget.GetPosition() + _followOffset;

            if (_followHard)
            {
                // Snap every frame: set grid pos, convert immediately, done.
                movementState.Position = targetGrid;
                _controller.Step(this, ref movementState, 0f);      // Grid→Pixel SetPosition() happens in Step
            }
            else
            {
                // Soft: only re-issue if not already within epsilon to avoid jitter/ping-pong.
                var to = targetGrid - movementState.Position;
                if (to.LengthSquared() > _followSnapEps * _followSnapEps)
                {
                    _controller.ScheduleMoveToward(ref movementState, targetGrid, _followSpeedTilesPerSec, _followSnapEps);
                }

                // Advance scripted if any (toward); otherwise, let physics (rare) run.
                if (!_controller.AdvanceScripted(this, ref movementState, dt) && movementState.HasMotion)
                    _controller.Step(this, ref movementState, dt);
            }

            base.Update(tick);
            return; // we handled our movement path
        }

        // --- Original movement paths when not following ---
        if (!_controller.AdvanceScripted(this, ref movementState, dt) && movementState.HasMotion)
            _controller.Step(this, ref movementState, dt);

        base.Update(tick);
    }

    #region public "scripted" movement; independent of velocity/accel

    public void MoveTo(Vector2 target, float seconds, Func<float, float>? easing = null, float snapEpsilon = 0.5f)
        => _controller.ScheduleMoveTo(ref movementState, target, seconds, easing, snapEpsilon);

    public void MoveToward(Vector2 targetPx, float speedPerSec, float snapEpsilon = 0.5f)
        => _controller.ScheduleMoveToward(ref movementState, targetPx, speedPerSec, snapEpsilon);

    public void StopMoveTo() => _controller.CancelScript(ref movementState);
    public void StopMoveToward() => _controller.CancelScript(ref movementState);

    #endregion public "scripted" movement; independent of velocity/accel

    #region IMovementStateMutator implementation

    public void SetVelocity(Vector2 v)
    {
        _controller.CancelScript(ref movementState);
        movementState.Velocity = v;
    }

    public void SetAcceleration(Vector2 a)
    {
        _controller.CancelScript(ref movementState);
        movementState.Acceleration = a;
    }

    public void StopMovement()
    {
        _controller.CancelScript(ref movementState);
        movementState.Velocity = Vector2.Zero;
        movementState.Acceleration = Vector2.Zero;
    }

    public void SetMaxSpeed(float? maxSpeed) => movementState.MaxSpeed = maxSpeed;

    public void SetLinearDamping(float dampingPerSec) => movementState.LinearDamping = MathF.Max(0f, dampingPerSec);

    #endregion IMovementStateMutator
}
