using System.Drawing;
using System.Numerics;
using Gondwana.Movement;
using Gondwana.Rendering;
using Gondwana.Timers;

namespace Gondwana.Drawing.Direct;

public abstract class DirectDrawingMovableBase : DirectDrawingBase, IMovable
{
    // --- Motion fields (pixel space) ---
    private readonly MovementController _controller;
    private MovementState movementState;
    private bool _motionActive;
    private Vector2 _scriptedTarget;
    private float _scriptedElapsed, _scriptedDuration;

    // --- MoveTo state (duration-based tween) ---
    private Func<float, float>? _scriptedEasing;    // easing curve delegate (e.g. EaseInOut)
    private float _scriptedSnapEpsilon = 0.5f;      // snap threshold in px or units

    // --- MoveToward state (constant-speed toward a target) ---
    private bool _towardActive;
    private Vector2 _towardTarget;
    private float _towardSpeedPerSec;
    private float _towardSnapEpsilon = 0.5f;        // px; tune as you like

    protected DirectDrawingMovableBase(RenderSurfaceHostBase renderSurfaceHost, Rectangle bounds)
        : base(renderSurfaceHost, bounds)
    {
        _controller = DirectDrawingManager.Instance.MovementController;
        // initialize motion at current position (px)
        movementState = MovementState.ForPixel(new Vector2(bounds.X, bounds.Y));
    }

    public CoordinateSpace PositionSpace => CoordinateSpace.Pixel;

    public Vector2 GetPosition() => new(Bounds.X, Bounds.Y);

    public void SetPosition(Vector2 p)
    {
        ForceRefresh();
        Bounds = new Rectangle((int)p.X, (int)p.Y, Bounds.Width, Bounds.Height);
        ForceRefresh();
    }

    protected internal override void Update(long tick)
    {
        if (tick == _lastTick)
            return;

        float dt = HighResTimer.GetDuration(_lastTick, tick);

        // 1) Duration-based scripted move (MoveTo)
        if (_motionActive)
        {
            _scriptedElapsed += dt;

            bool finished = _controller.MoveTo(
                (IMovable)this, ref movementState,
                _scriptedTarget, _scriptedDuration, ref _scriptedElapsed,
                _scriptedEasing, _scriptedSnapEpsilon
            );

            if (finished)
                _motionActive = false;
        }
        // 2) Constant-speed MoveToward (uses controller each frame)
        else if (_towardActive)
        {
            bool finished = _controller.MoveToward(
                (IMovable)this, ref movementState,
                _towardTarget, _towardSpeedPerSec, dt, _towardSnapEpsilon
            );

            if (finished)
                _towardActive = false;
        }
        // 3) Physics-style (velocity/accel/damping)
        else if (movementState.HasMotion)
        {
            _controller.Step(this, ref movementState, dt);
        }

        base.Update(tick);
    }

    #region public shims for MovementController methods

    public void MoveTo(Vector2 target, float seconds,
                   Func<float, float>? easing = null,
                   float snapEpsilon = 0.5f)
    {
        // cancel scripted MoveToward if it was running
        _towardActive = false;
        
        _scriptedTarget = target;
        _scriptedDuration = seconds;
        _scriptedElapsed = 0f;
        _scriptedEasing = easing;
        _scriptedSnapEpsilon = snapEpsilon;
        _motionActive = true;
    }

    /// <summary>
    /// Move toward a pixel target at a constant speed (px/sec). Stops when within snapEpsilon.
    /// Cancels any active MoveTo() tween.
    /// </summary>
    public void MoveToward(Vector2 targetPx, float speedPerSec, float snapEpsilon = 0.5f)
    {
        // cancel scripted MoveTo if it was running
        _motionActive = false;

        _towardTarget = targetPx;
        _towardSpeedPerSec = MathF.Max(0f, speedPerSec);
        _towardSnapEpsilon = MathF.Max(0f, snapEpsilon);
        _towardActive = true;
    }

    public void StopMoveTo()
    {
        _motionActive = false;
        // leave velocity/accel alone; caller can also call Stop() if desired
    }

    /// <summary>Stops MoveToward immediately.</summary>
    public void StopMoveToward()
    {
        _towardActive = false;
        // leave velocity/accel alone; caller can also call Stop() if desired
    }

    #endregion public shims for MovementController methods
}
