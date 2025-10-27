using System.Drawing;
using System.Numerics;
using Gondwana.Movement;
using Gondwana.Rendering;
using Gondwana.Timers;

namespace Gondwana.Drawing.Direct;

public abstract class DirectDrawingMovableBase : DirectDrawingBase, IMovable
{
    private MovementState _movementState;

    // update timing for fixed-step physics
    private float _accum;
    private const float _fixedDt = 1f / 240f;
    private const int _maxSubsteps = 8;

    protected DirectDrawingMovableBase(RenderSurfaceHostBase host, Rectangle bounds)
        : base(host, bounds)
    {
        _movementState = MovementState.ForPixel(new Vector2(bounds.X, bounds.Y));
        Movement = new MovementController(this, _movementState);
    }

    public MovementController Movement { get; }

    public CoordinateSpace PositionSpace => CoordinateSpace.Pixel;

    public Vector2 GetPosition() => _movementState.Position;

    public void SetPosition(Vector2 p)
    {
        // keep MovementState and Bounds in sync
        _movementState.Position = p;

        Bounds = new Rectangle(
            (int)Math.Round(p.X),
            (int)Math.Round(p.Y),
            Bounds.Width,
            Bounds.Height);

        ForceRefresh();
    }

    // ---------------------------------------------------------------------
    // Per-frame update
    // ---------------------------------------------------------------------
    protected internal override void Update(long tick)
    {
        if (tick == _lastTick)
            return;

        float dt = HighResTimer.GetDuration(_lastTick, tick);
        _accum += dt;

        int steps = 0;
        while (_accum >= _fixedDt && steps < _maxSubsteps)
        {
            // run scripted movement first; if none active, apply physics
            if (!Movement.AdvanceScripted(dt))
                Movement.Step(dt);

            _accum -= _fixedDt;
            steps++;
        }

        base.Update(tick);
    }
}
