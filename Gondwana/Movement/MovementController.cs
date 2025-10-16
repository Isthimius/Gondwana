using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Scenes;

namespace Gondwana.Movement;

public sealed class MovementController
{
    private readonly ISceneLayerCoordinates _coords;
    private readonly SceneLayer _layer;

    public MovementController(SceneLayer screenLayer)
    {
        _layer = screenLayer ?? throw new ArgumentNullException(nameof(screenLayer));
        _coords = _layer.CoordinateSystem ?? throw new ArgumentNullException(nameof(screenLayer.CoordinateSystem));
    }

    public void Step(IMovable mover, ref MovementState moveState, float dt)
    {
        // Integrate (semi-implicit Euler)
        moveState.Velocity += moveState.Acceleration * dt;
        moveState.ClampVelocity();

        // Exponential damping (frame-rate independent)
        if (moveState.LinearDamping > 0f)
            moveState.Velocity *= MathF.Exp(-moveState.LinearDamping * dt);

        moveState.Position += moveState.Velocity * dt;

        // Optional wrapping in GRID space
        if (moveState.MovementSpace == CoordinateSpace.Grid && (moveState.WrapX || moveState.WrapY))
        {
            var wrapped = _coords.FindEquivalentSceneLayerCoordinates(
                new PointF(moveState.Position.X, moveState.Position.Y), 
                _layer.GridColumnCount - 1,
                _layer.GridRowCount - 1
            );
            moveState.Position = new Vector2(wrapped.X, wrapped.Y);
        }

        // === Apply ===

        // Same-space fast path
        if (moveState.MovementSpace == mover.PositionSpace)
        {
            mover.SetPosition(moveState.Position);
            return;
        }

        // Grid (state) -> Pixel (mover): e.g., world-anchored overlay following a sprite
        if (moveState.MovementSpace == CoordinateSpace.Grid && mover.PositionSpace == CoordinateSpace.Pixel)
        {
            var px = _coords.GetAnchorPixelAtSceneLayerCoordinates(
                _layer,
                new PointF(moveState.Position.X, moveState.Position.Y)
            );
            mover.SetPosition(new Vector2(px.X, px.Y));
            return;
        }

        // Pixel (state) -> Grid (mover): e.g., mouse/drag/minimap driving a world entity
        if (moveState.MovementSpace == CoordinateSpace.Pixel && mover.PositionSpace == CoordinateSpace.Grid)
        {
            // Prefer a PointF-taking API if available to avoid early truncation.
            var gp = _coords.GetSceneLayerCoordinatesAtPixel(
                _layer,
                new Point((int)moveState.Position.X, (int)moveState.Position.Y)
            );
            mover.SetPosition(new Vector2(gp.X, gp.Y));
            return;
        }

        // If we ever add new spaces, fail loudly now rather than mis-convert
        throw new InvalidOperationException($"Unsupported MovementSpace conversion: {moveState.MovementSpace} -> {mover.PositionSpace}");
    }
}
