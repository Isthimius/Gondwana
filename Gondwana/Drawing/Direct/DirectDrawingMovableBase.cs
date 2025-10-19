using System.Drawing;
using System.Numerics;
using Gondwana.Movement;
using Gondwana.Rendering;
using Gondwana.Timers;

namespace Gondwana.Drawing.Direct;

public abstract class DirectDrawingMovableBase : DirectDrawingBase, IMovable, IMovementStateMutator
{
    // --- Motion fields (pixel space) ---
    private readonly MovementController _controller;
    private MovementState movementState;

    protected DirectDrawingMovableBase(RenderSurfaceHostBase renderSurfaceHost, Rectangle bounds)
        : base(renderSurfaceHost, bounds)
    {
        _controller = DirectDrawingManager.Instance.MovementController;
        // initialize motion at current position (px)
        movementState = MovementState.ForPixel(new Vector2(bounds.X, bounds.Y));
    }

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

        float dt = HighResTimer.GetDuration(_lastTick, tick);

        // Let controller handle ANY scripted motion; if it runs, we’re done for this frame.
        bool scriptedRan = _controller.AdvanceScripted(this, ref movementState, dt); // controller owns scripted logic

        if (!scriptedRan && movementState.HasMotion)        // else physics path
            _controller.Step(this, ref movementState, dt);  // physics, wrapping, space conversion live here

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
