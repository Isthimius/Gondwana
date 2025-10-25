using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Scenes;

namespace Gondwana.Movement;

public sealed partial class MovementController : IDisposable
{
    private readonly ISceneLayerCoordinates? _coords;
    private readonly SceneLayer? _sceneLayer;
    private readonly IMovable _target;
    private MovementState _state;

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

            if (_followHard)
            {
                SetPosition(goal);
                return true;
            }

            // if easing-based follow is configured, use a TweenTo
            if (_followEasing is not null && _followDurationSec > 0f)
            {
                ScheduleMoveTo(ref _state, goal, _followDurationSec, _followEasing, _followSnapPx);
            }
            else
            {
                // Existing path: constant-speed pursue
                ScheduleMoveToward(ref _state, goal, _followSpeedPxPerSec, _followSnapPx);
            }
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
                // tiles/sec → px/sec (sample +X; first-order approximation)
                var pxRight = coords.GetAnchorPixelAtSceneLayerCoordinates(
                                  layer, new System.Drawing.PointF(grid.X + 1f, grid.Y));
                float pxPerTile = MathF.Max(1f,
                    new Vector2(pxRight.X - pxNow.X, pxRight.Y - pxNow.Y).Length());

                float speedPxPerSec = _followSpeedTilesPerSec * pxPerTile;
                float snapPx = _followSnapTiles * pxPerTile;

                ScheduleMoveToward(ref _state, goalPx, speedPxPerSec, snapPx);
            }
            // fall through into scripted advance
        }

        // Internal script engine (TweenTo/Toward based on MovementScriptType)
        return AdvanceScripted(_target, ref _state, dt);
    }


    public void SetVelocity(Vector2 v) { CancelScript(ref _state); _state.Velocity = v; }             // stop script on manual control
    public void SetAcceleration(Vector2 a) { CancelScript(ref _state); _state.Acceleration = a; }
    public void StopMovement() { CancelScript(ref _state); _state.Velocity = Vector2.Zero; _state.Acceleration = Vector2.Zero; }
    public void SetMaxSpeed(float? maxSpeed) { _state.MaxSpeed = maxSpeed; }
    public void SetLinearDamping(float dampingPerSec) { _state.LinearDamping = MathF.Max(0f, dampingPerSec); }

    public void SetWrapX(bool enabled) => _state.WrapX = enabled;
    public void SetWrapY(bool enabled) => _state.WrapY = enabled;

    public void Dispose()
    {
        ScriptedMovementStopped = null;
    }
}
