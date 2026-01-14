using System.Drawing;
using System.Numerics;
using Gondwana.Movement;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Gondwana.Timers;

namespace Gondwana.Drawing.Direct;

public abstract class DirectDrawingMovableBase : DirectDrawingBase, IMovable
{
    // update timing for fixed-step physics
    private float _accum;
    private const float _fixedDt = 1f / 240f;
    private const int _maxSubsteps = 8;

    protected DirectDrawingMovableBase(RenderSurfaceHostBase renderSurfaceHost,
                                       DirectDrawingMode mode,
                                       SceneLayer? sceneLayer,
                                       View? view,
                                       Rectangle? screenBounds,
                                       Rectangle? worldBounds,
                                       string? name = null)
        : base(renderSurfaceHost, mode, sceneLayer, view, screenBounds, worldBounds, name)
    {
        Rectangle bounds = (mode == DirectDrawingMode.SceneLayer ? worldBounds : screenBounds)!.Value;

        var movementState = MovementState.ForPixel(new Vector2(bounds.X, bounds.Y));
        Movement = new MovementController(this, movementState);
    }

    public MovementController Movement { get; }

    public MovementSpace PositionSpace => MovementSpace.Pixel;

    public Vector2 GetPosition()
    {
        Rectangle r = (Mode == DirectDrawingMode.SceneLayer)
            ? WorldBounds
            : ScreenBounds;

        return new Vector2(r.X, r.Y);
    }

    public void SetPosition(Vector2 p)
    {
        int x = (int)Math.Round(p.X);
        int y = (int)Math.Round(p.Y);

        if (Mode == DirectDrawingMode.SceneLayer)
        {
            var r = WorldBounds;
            WorldBounds = new Rectangle(x, y, r.Width, r.Height);
        }
        else
        {
            var r = ScreenBounds;
            ScreenBounds = new Rectangle(x, y, r.Width, r.Height);
        }

        ForceRefresh();
    }

    // ---------------------------------------------------------------------
    // Per-frame update
    // ---------------------------------------------------------------------
    public override void Update(long tick)
    {
        if (tick <= _lastTick)
            return;

        // 1) clamp giant stalls (alt-tab, debugger break, GC, etc.)
        const float MaxFrameDt = 1f / 15f; // ~66ms
        float dt = MathF.Min(HighResTimer.GetDuration(_lastTick, tick), MaxFrameDt);

        // 2) accumulate time
        _accum += dt;

        int steps = 0;
        while (_accum >= _fixedDt && steps < _maxSubsteps)
        {
            // Always integrate at the fixed step (not dt!)
            Movement.AdvanceMovement(_fixedDt);

            _accum -= _fixedDt;
            steps++;
        }

        // 3) if we hit the cap, drop remainder so we don't spiral
        if (steps == _maxSubsteps)
            _accum = 0f;

        base.Update(tick);
    }
}
