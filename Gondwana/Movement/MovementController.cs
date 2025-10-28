using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Scenes;

namespace Gondwana.Movement;

public sealed partial class MovementController : IDisposable
{
    private readonly ISceneLayerCoordinates? _coords;
    private readonly SceneLayer? _sceneLayer;
    private readonly IMovable _mover;
    private MovementState _state;

    public event Action? ScriptedMovementStopped;

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

    public MovementState MovementState => _state;

    public bool IsFollowing => _followHard || _followPixel is not null || _followGridTarget is not null;

    public bool IsScripted => _state.Script.Type != Scripted.MovementScriptType.None;

    public bool IsIntegratedActive => !IsFollowing && !IsScripted && _state.HasMotion;

    public void SetWrapX(bool enabled) => _state.WrapX = enabled;

    public void SetWrapY(bool enabled) => _state.WrapY = enabled;

    public void StopAllMovement()
    {
        CancelScript();
        _state.Velocity = Vector2.Zero;
        _state.Acceleration = Vector2.Zero;
    }

    public void Dispose()
    {
        ScriptedMovementStopped = null;
    }
}
