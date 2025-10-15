using Gondwana.Drawing.Coordinates;
using Gondwana.Scenes;

namespace Gondwana.Movement;

public sealed class MovementController
{
    private readonly ISceneLayerCoordinates _coords; // per-layer impl
    private readonly SceneLayer _screenLayer;

    public MovementController(SceneLayer screenLayer)
    {
        _screenLayer = screenLayer ?? throw new ArgumentNullException(nameof(screenLayer));
        _coords = _screenLayer.CoordinateSystem;
    }

    public void Step(IMovable target, ref MotionState m, float dtSeconds)
    {
        // Pull current grid-space position from the target via its adapter
        var p = target.GetGridPosition();

        // Semi-implicit Euler (stable enough for games)
        m.Velocity += m.Acceleration * dtSeconds;
        m.ClampVelocity();

        // Apply linear damping (simple exponential decay)
        if (m.LinearDamping > 0f)
            m.Velocity *= MathF.Max(0f, 1f - m.LinearDamping * dtSeconds);

        p += m.Velocity * dtSeconds;

        // Optional wrapping using the coord system’s canonicalizer
        if (m.WrapX || m.WrapY)
        {
            var eq = _coords.FindEquivalentSceneLayerCoordinates(
                new System.Drawing.PointF(p.X, p.Y),
                _screenLayer.GridColumnCount - 1, _screenLayer.GridRowCount - 1);
            
            // Respect only the axes you asked to wrap
            p = new System.Numerics.Vector2(
                m.WrapX ? eq.X : p.X,
                m.WrapY ? eq.Y : p.Y);
        }

        // Push updated grid-space position back to the target
        target.SetGridPosition(p);
    }
}
