using System.Drawing;
using System.Numerics;
using Gondwana.Movement;
using Gondwana.Rendering;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Direct;

public abstract class DirectDrawingMovableBase : DirectDrawingBase, IScriptedMovementListener
{
    // add the tiny private adapter inside the class
    private sealed class LocalMovable : IMovable
    {
        private readonly DirectDrawingMovableBase _o;
        public LocalMovable(DirectDrawingMovableBase o) { _o = o; }
        public CoordinateSpace PositionSpace => CoordinateSpace.Pixel;
        public Vector2 GetPosition() => _o._movementState.Position;
        public void SetPosition(Vector2 p)
        {
            _o._movementState.Position = p;
            _o._bounds = new Rectangle((int)Math.Round(p.X), (int)Math.Round(p.Y), _o._bounds.Width, _o._bounds.Height);
            _o.ForceRefresh();
        }
    }

    // --- Motion fields (pixel space) ---
    private MovementState _movementState;
    public MovementController Movement { get; private set; }

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
        _movementState = MovementState.ForPixel(new Vector2(_bounds.X, _bounds.Y));
        IMovable target = new LocalMovable(this);
        Movement = new MovementController(target, _movementState);
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
        Movement.CancelScript(ref _movementState);
    }

    // --- glue: infer layer and initialize a grid-state controller once ---
    private void EnsureFollowBinding(IMovableOnSceneLayer target, Vector2 gridOffset)
    {
        _followTarget = target;
        _followLayer = target.SceneLayer;
        _followOffset = gridOffset;

        //_controller = MovementController.ForSceneLayer(_followLayer);
        var startGrid = target.GetPosition() + _followOffset;
        _movementState = MovementState.ForSceneLayer(startGrid);
        Movement.Step(_followTarget, ref _movementState, 0f);
    }

    #endregion sprite / camera follow support

    /// <summary>
    /// Gets a copy of the current <see cref="MovementState"/> representing
    /// this object's motion parameters and position. Changes made to the
    /// returned struct do not affect the internal state.
    /// Use <see cref="IMovementStateMutator"/> methods to modify motion.
    /// </summary>
    public MovementState MovementState => _movementState;

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
                _movementState.Position = targetGrid;
                Movement.Step(this, ref _movementState, 0f);      // Grid→Pixel SetPosition() happens in Step
            }
            else
            {
                // Soft: only re-issue if not already within epsilon to avoid jitter/ping-pong.
                var to = targetGrid - _movementState.Position;
                if (to.LengthSquared() > _followSnapEps * _followSnapEps)
                {
                    Movement.ScheduleMoveToward(ref _movementState, targetGrid, _followSpeedTilesPerSec, _followSnapEps);
                }

                // Advance scripted if any (toward); otherwise, let physics (rare) run.
                if (!Movement.AdvanceScripted(this, ref _movementState, dt) && _movementState.HasMotion)
                    Movement.Step(this, ref _movementState, dt);
            }

            base.Update(tick);
            return; // we handled our movement path
        }

        // --- Original movement paths when not following ---
        if (!Movement.AdvanceScripted(this, ref _movementState, dt) && _movementState.HasMotion)
            Movement.Step(this, ref _movementState, dt);

        base.Update(tick);
    }
}
