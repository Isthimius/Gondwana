using System.Drawing;
using System.Numerics;
using Gondwana.Movement;

namespace Gondwana.Scenes;

/// <summary>
/// A grid-space camera that can hard- or soft-follow a target on a SceneLayer.
/// Replaces legacy "scroll binding" with controller-driven movement.
/// </summary>
public sealed class Camera : IMovableOnSceneLayer
{
    private readonly SceneLayer _layer;
    private readonly MovementController _controller;
    private MovementState _state;

    // Follow state
    private IMovableOnSceneLayer? _target;
    private Vector2 _offsetGrid;
    private bool _hardFollow;
    private float _softSpeedTilesPerSec;
    private float _softSnapEps = 0.25f;

    private float _scriptElapsed, _scriptDuration;
    private float _scriptSnapEps = 0.25f;
    private Func<float, float>? _scriptEasing;
    private Vector2 _scriptTarget;

    // Optional constraints (grid units)
    private RectangleF? _deadZoneGrid;   // camera only moves when target exits this zone (relative to camera)
    private RectangleF? _worldBoundsGrid; // clamp camera origin within these bounds

    public Camera(SceneLayer layer, Vector2? initialGridPos = null)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        //_controller = MovementController.ForSceneLayer(_layer);                  // layer-aware controller
        var start = initialGridPos ?? new Vector2(_layer.SourceSceneLayerTile.X, _layer.SourceSceneLayerTile.Y);
        _state = MovementState.ForSceneLayer(start);                             // grid state
        // apply immediately
        SetPosition(_state.Position);
    }

    // IMovableOnSceneLayer
    public SceneLayer SceneLayer => _layer;
    public CoordinateSpace PositionSpace => CoordinateSpace.Grid;               // camera lives in grid space
    public Vector2 GetPosition() => _state.Position;

    public void SetPosition(Vector2 pos)
    {
        _state.Position = pos;
        // Apply to the SceneLayer (camera = source tile)
        _layer.SourceSceneLayerTile = new PointF(pos.X, pos.Y);                 // mirrors CameraMovable semantics
    }

    #region *** follow API ***

    public IMovableOnSceneLayer? FollowTarget => _target;

    public void FollowHard(IMovableOnSceneLayer target, Vector2 gridOffset = default)
    {
        _target = target;
        _offsetGrid = gridOffset;
        _hardFollow = true;
        _controller.CancelScript(ref _state);
        SnapNow(); // snap immediately to starting position
    }

    public void FollowSoft(IMovableOnSceneLayer target, float speedTilesPerSec, float snapEpsilon = 0.25f, Vector2 gridOffset = default)
    {
        _target = target;
        _offsetGrid = gridOffset;
        _hardFollow = false;
        _softSpeedTilesPerSec = MathF.Max(0f, speedTilesPerSec);
        _softSnapEps = MathF.Max(0f, snapEpsilon);
        _controller.CancelScript(ref _state);
        // schedule first leg; subsequent legs happen in Update
        var desired = DesiredCameraOrigin();
        _controller.ScheduleMoveToward(ref _state, desired, _softSpeedTilesPerSec, _softSnapEps);
    }

    public void Unfollow()
    {
        _target = null;
        _controller.CancelScript(ref _state);
    }

    /// <summary>Sets a dead-zone rectangle (in grid units) centered on the camera. Camera only moves when the target exits this region.</summary>
    public void SetDeadZone(RectangleF deadZoneGrid) => _deadZoneGrid = deadZoneGrid;

    public void ClearDeadZone() => _deadZoneGrid = null;

    #endregion *** follow API ***

    /// <summary>Clamps the camera origin within world bounds (in grid units).</summary>
    public void SetWorldBounds(RectangleF worldBoundsGrid) => _worldBoundsGrid = worldBoundsGrid;

    public void ClearWorldBounds() => _worldBoundsGrid = null;

    // Duration-based tween to a grid target (optional easing)
    public void MoveTo(Vector2 targetGrid, float durationSec,
                       Func<float, float>? easing = null, float snapEpsilon = 0.25f)
    {
        _hardFollow = false;         // scripted move overrides follow
        _target = null;
        _controller.CancelScript(ref _state);
        _scriptElapsed = 0f;
        _scriptDuration = MathF.Max(0f, durationSec);
        _scriptSnapEps = MathF.Max(0f, snapEpsilon);
        _scriptEasing = easing;

        _scriptTarget = targetGrid;  // remember for re-entrancy if desired
                                     // first frame is advanced in Update via AdvanceScripted
    }

    // Constant-speed move toward a grid target
    public void MoveToward(Vector2 targetGrid, float tilesPerSec, float snapEpsilon = 0.25f)
    {
        _hardFollow = false;
        _target = null;
        _controller.CancelScript(ref _state);
        _softSnapEps = MathF.Max(0f, snapEpsilon);
        _softSpeedTilesPerSec = MathF.Max(0f, tilesPerSec);

        // schedule first leg; Update will advance and re-schedule as needed
        _controller.ScheduleMoveToward(ref _state, targetGrid, _softSpeedTilesPerSec, _softSnapEps);
        _scriptTarget = targetGrid;
    }

    /// <summary>Advance camera one frame. Call from your engine loop.</summary>
    public void Update(float dtSeconds)
    {
        // 1) Scripted MoveTo (duration-based) has priority
        if (_scriptDuration > 0f)
        {
            _scriptElapsed += dtSeconds;
            //if (_controller.MoveTo(this, ref _state, _scriptTarget, _scriptDuration,
            //                       ref _scriptElapsed, _scriptEasing, _scriptSnapEps))
            //{
            //    _scriptDuration = 0f; // finished
            //}

            return; // scripted handled this frame
        }

        // 2) Follow logic (hard/soft)
        if (_target is not null)
        {
            if (_hardFollow)
            {
                // Snap each frame with dead-zone awareness
                var newOrigin = DesiredCameraOriginWithDeadZone();
                SetPosition(ClampToWorld(newOrigin));
            }
            else
            {
                // Soft follow: re-issue toward current desired origin if far enough
                var desired = DesiredCameraOrigin();
                var delta = desired - _state.Position;

                if (delta.LengthSquared() > _softSnapEps * _softSnapEps)
                    _controller.ScheduleMoveToward(ref _state, ClampToWorld(desired),
                                                   _softSpeedTilesPerSec, _softSnapEps);

                // Advance the scheduled tween/toward; else run physics if any
                if (!_controller.AdvanceScripted(this, ref _state, dtSeconds) && _state.HasMotion)
                    _controller.Step(this, ref _state, dtSeconds);
            }

            return; // follow handled this frame
        }

        // 3) Neither scripted nor following: let inertial/physics glide
        if (_state.HasMotion)
            _controller.Step(this, ref _state, dtSeconds);
    }

    #region private methods

    private void SnapNow()
    {
        if (_target is null) return;
        var p = DesiredCameraOriginWithDeadZone();
        SetPosition(ClampToWorld(p));
    }

    private Vector2 DesiredCameraOrigin()
    {
        var targetPos = _target!.GetPosition() + _offsetGrid;                    // grid target + offset
        return targetPos;
    }

    private Vector2 DesiredCameraOriginWithDeadZone()
    {
        var desired = DesiredCameraOrigin();
        if (_deadZoneGrid is null)
            return desired;

        // dead-zone is defined relative to current camera origin. If target is inside, keep current origin.
        var cam = _state.Position;
        var dz = _deadZoneGrid.Value;
        var dzLeft = cam.X + dz.Left;
        var dzRight = cam.X + dz.Right;
        var dzTop = cam.Y + dz.Top;
        var dzBottom = cam.Y + dz.Bottom;

        var tx = desired.X;
        var ty = desired.Y;

        float nx = cam.X;
        float ny = cam.Y;

        if (tx < dzLeft) nx += tx - dzLeft;
        if (tx > dzRight) nx += tx - dzRight;
        if (ty < dzTop) ny += ty - dzTop;
        if (ty > dzBottom) ny += ty - dzBottom;

        return new Vector2(nx, ny);
    }

    private Vector2 ClampToWorld(Vector2 pos)
    {
        if (_worldBoundsGrid is null)
            return pos;

        var b = _worldBoundsGrid.Value;
        var x = Math.Clamp(pos.X, b.Left, b.Right);
        var y = Math.Clamp(pos.Y, b.Top, b.Bottom);

        return new Vector2(x, y);
    }

    #endregion private methods
}
