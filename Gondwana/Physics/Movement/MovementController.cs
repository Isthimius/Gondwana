using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Physics.Movement;
using Gondwana.Physics.Movement.Scripted;
using Gondwana.Scenes;

namespace Gondwana.Movement;

/// <summary>
/// Central controller responsible for managing all movement modes for a single <see cref="IMovable"/> object.
/// Combines three systems:
/// <list type="bullet">
/// <item><description><b>Follow</b> – hard/soft following of pixel or grid targets.</description></item>
/// <item><description><b>Scripted</b> – tween or constant-speed motion toward a destination.</description></item>
/// <item><description><b>Integrated</b> – free physics-style integration (velocity and acceleration).</description></item>
/// </list>
/// The controller automatically selects the correct behavior each frame internally through <see cref="AdvanceMovement(float)"/>.
/// </summary>
public sealed partial class MovementController : IDisposable
{
    private readonly ISceneLayerCoordinates? _coords;
    private readonly SceneLayer? _sceneLayer;
    private readonly IMovable _mover;
    private MovementState _state;

    /// <summary>
    /// Raised when a scripted movement (tween or MoveToward) begins.
    /// Subscribers are notified exactly once per script initialization.
    /// </summary>
    public event Action<ScriptedMovement>? ScriptedMovementStarted;

    /// <summary>
    /// Raised when a scripted movement (tween or MoveToward) completes or is explicitly cancelled.
    /// Subscribers are notified exactly once per script termination.
    /// </summary>
    public event Action<ScriptedMovement>? ScriptedMovementStopped;

    // Bind to one target + initial state. Optional layer when you need Grid↔Pixel & wrapping.
    internal MovementController(IMovable mover, MovementState initial, SceneLayer? layer = null)
    {
        _mover = mover;
        _state = initial;

        if (layer is not null)
        {
            _sceneLayer = layer;
            _coords = layer.CoordinateSystem ?? throw new ArgumentException("SceneLayer must have a CoordinateSystem.", nameof(layer));
        }
    }

    /// <summary>
    /// Advances the movement controller by one frame.
    /// Executes follow, scripted, or integrated motion in priority order:
    /// <list type="number">
    /// <item><description>Follow (if active)</description></item>
    /// <item><description>Scripted (if active)</description></item>
    /// <item><description>Integrated (physics)</description></item>
    /// </list>
    /// </summary>
    /// <param name="dt">Elapsed time in seconds since the previous frame.</param>
    /// <returns>
    /// <see langword="true"/> if the frame was consumed by a follow or scripted motion;
    /// otherwise <see langword="false"/> to continue with integrated motion.
    /// </returns>
    internal bool AdvanceMovement(float dt)
    {
        // follow owns the frame if engaged
        if (AdvanceFollow())
            return true;

        // scripted tween/toward runs next
        if (AdvanceScripted(dt))
            return true;

        // finally physics integration
        return AdvanceIntegrated(dt);
    }

    /// <summary>
    /// Gets the current <see cref="MovementState"/> containing velocity, acceleration,
    /// and motion flags for the controlled object.
    /// </summary>
    public MovementState MovementState => _state;

    /// <summary>
    /// Indicates whether the controller is currently engaged in a follow behavior.
    /// Returns <see langword="true"/> if a pixel or grid target is set, or if hard follow is active.
    /// </summary>
    public bool IsFollowing => _followHard || _followPixel is not null || _followGridTarget is not null;

    /// <summary>
    /// Indicates whether a scripted movement (tween or MoveToward) is currently active.
    /// </summary>
    public bool IsScripted => _state.Script.Type != MovementScriptType.None;

    /// <summary>
    /// Indicates whether physics-style integration is currently active.
    /// Returns <see langword="true"/> when the controller is not following or scripted,
    /// and <see cref="MovementState.HasMotion"/> is <see langword="true"/>.
    /// </summary>
    public bool IsIntegratedActive => !IsFollowing && !IsScripted && _state.HasMotion;

    /// <summary>
    /// Enables or disables horizontal world wrapping.
    /// When enabled, movement crossing the left/right edges wraps the IMovable to the opposite side.
    /// Only meaningful for Grid space; ignored for Pixel space.
    /// </summary>
    public bool WrapX { get; internal set; } = false;

    /// <summary>
    /// Enables or disables vertical world wrapping.
    /// When enabled, movement crossing the top/bottom edges wraps the IMovable to the opposite side.
    /// Only meaningful for Grid space; ignored for Pixel space.
    /// </summary>
    public bool WrapY { get; internal set; } = false;

    /// <summary>
    /// Immediately stops all forms of movement — follow, scripted, and integrated.
    /// Cancels active tweens, clears velocity and acceleration, and halts motion this frame.
    /// </summary>
    public void StopAllMovement()
    {
        CancelScript();
        _state.Velocity = Vector2.Zero;
        _state.Acceleration = Vector2.Zero;
    }

    /// <summary>
    /// Releases resources and detaches all event handlers.
    /// </summary>
    public void Dispose()
    {
        ScriptedMovementStarted = null;
        ScriptedMovementStopped = null;
    }
}
