using System;
using System.Drawing;
using System.Numerics;
using Gondwana.Movement;
using Gondwana.Rendering;
using Gondwana.Timers;

namespace Gondwana.Drawing.Direct;

public abstract class DirectDrawingMovableBase : DirectDrawingBase
{
    private MovementState _movementState;
    public MovementController Movement { get; }

    protected DirectDrawingMovableBase(RenderSurfaceHostBase host, Rectangle bounds)
        : base(host, bounds)
    {
        _movementState = MovementState.ForPixel(new Vector2(bounds.X, bounds.Y));
        IMovable target = new LocalMovable(this);
        Movement = new MovementController(target, _movementState);
    }

    // ---------------------------------------------------------------------
    // Private adapter that the MovementController uses to move this drawable
    // ---------------------------------------------------------------------
    private sealed class LocalMovable : IMovable
    {
        private readonly DirectDrawingMovableBase _owner;

        public LocalMovable(DirectDrawingMovableBase owner)
        {
            _owner = owner;
        }

        public CoordinateSpace PositionSpace => CoordinateSpace.Pixel;

        public Vector2 GetPosition() => _owner._movementState.Position;

        public void SetPosition(Vector2 p)
        {
            _owner._movementState.Position = p;

            _owner.Bounds = new Rectangle(
                (int)Math.Round(p.X),
                (int)Math.Round(p.Y),
                _owner.Bounds.Width,
                _owner.Bounds.Height);

            _owner.ForceRefresh();
        }
    }

    // ---------------------------------------------------------------------
    // Per-frame update
    // ---------------------------------------------------------------------
    protected internal override void Update(long tick)
    {
        if (tick == _lastTick)
            return;

        float dt = HighResTimer.GetDuration(_lastTick, tick);

        // run scripted movement first; if none active, apply physics
        if (!Movement.AdvanceScripted(dt))
            Movement.Step(dt);

        base.Update(tick);
    }
}
