using System.Drawing;
using System.Numerics;
using Gondwana.Movement;
using Gondwana.Rendering;
using Gondwana.Timers;

namespace Gondwana.Drawing.Direct;

public abstract class DirectDrawingMovableBase : DirectDrawingBase, IMovable
{
    // --- Motion fields (pixel space) ---
    private readonly MovementController _controller = MovementController.ForRenderSurface();    // pixel-only ctor
    private MovementState _motion = MovementState.ForPixel(Vector2.Zero);                       // default
    private bool _motionActive;
    private Vector2 _scriptedStart;
    private Vector2 _scriptedTarget;
    private float _scriptedElapsed, _scriptedDuration;

    protected DirectDrawingMovableBase(RenderSurfaceHostBase renderSurfaceHost, Rectangle bounds)
        : base(renderSurfaceHost, bounds)
    {
        // initialize motion at current position (px)
        _motion = MovementState.ForPixel(new Vector2(bounds.X, bounds.Y));
    }

    public CoordinateSpace PositionSpace => CoordinateSpace.Pixel;

    public Vector2 GetPosition() => new(Bounds.X, Bounds.Y);

    public void SetPosition(Vector2 p)
    {
        ForceRefresh();
        Bounds = new Rectangle((int)p.X, (int)p.Y, Bounds.Width, Bounds.Height);
        ForceRefresh();
    }

    /// <summary>Move the top-left to a pixel position over a duration (seconds), linear.</summary>
    public void MoveTo(Vector2 targetPx, float durationSec)
    {
        _scriptedStart = new Vector2(Bounds.X, Bounds.Y);
        _scriptedTarget = targetPx;
        _scriptedElapsed = 0f;
        _scriptedDuration = MathF.Max(0.0001f, durationSec);
        _motionActive = true;

        // zero out physics; we’ll drive position directly
        _motion.Velocity = Vector2.Zero;
        _motion.Acceleration = Vector2.Zero;

        ForceRefresh();
    }

    /// <summary>Instantly jump to a pixel position.</summary>
    public void JumpTo(Vector2 targetPx)
    {
        _motionActive = false;
        _motion.Position = targetPx;
        SetPosition(targetPx);
    }

    /// <summary>Set a constant pixel velocity (px/sec). Use Zero to stop.</summary>
    public void SetVelocity(Vector2 velocityPxPerSec, float? maxSpeed = null, float linearDampingPerSec = 0f)
    {
        _motion.Velocity = velocityPxPerSec;
        _motion.MaxSpeed = maxSpeed;
        _motion.LinearDamping = linearDampingPerSec;
        _motion.Acceleration = Vector2.Zero;
        _motionActive = false; // physics-driven, not scripted
    }

    protected internal override void Update(long tick)
    {
        // --- compute dt in seconds BEFORE base.Update sets _lastTick ---
        float dt = 0f;
        if (_lastTick is long last)
        {
            long deltaTicks = tick - last;

            if (deltaTicks < 0)
                deltaTicks = 0;

            dt = (float)(deltaTicks / (double)HighResTimer.TicksPerSecond);

            if (dt < 0f || dt > 1f)
                dt = 0f; // clamp outliers, same policy as your other Directs
        }

        // Scripted move (linear over time) → drive Position directly (no velocity math)
        if (_motionActive && dt > 0f)
        {
            _scriptedElapsed += dt;

            float t = Math.Clamp(_scriptedElapsed / _scriptedDuration, 0f, 1f);
            var pos = Vector2.Lerp(_scriptedStart, _scriptedTarget, t);
            _motion.Position = pos;

            // apply immediately; dt=0 applies without integrating
            _controller.Step(this, ref _motion, 0f);

            if (t >= 1f)
                _motionActive = false;
        }
        // Physics-style movement (constant velocity / damping)
        else if (dt > 0f && (_motion.Velocity != Vector2.Zero || _motion.Acceleration != Vector2.Zero))
        {
            _controller.Step(this, ref _motion, dt);
        }

        // Let base handle fades and housekeeping (also sets _lastTick = tick)
        base.Update(tick);
    }
}
