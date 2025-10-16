using System.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Scenes;

namespace Gondwana.Movement;

public sealed class MovementController
{
    private readonly ISceneLayerCoordinates? _coords; // per-layer impl
    private readonly SceneLayer? _screenLayer;

    private MovementController() { }

    public MovementController(SceneLayer screenLayer)
    {
        _screenLayer = screenLayer ?? throw new ArgumentNullException(nameof(screenLayer));
        _coords = _screenLayer.CoordinateSystem;
    }

    public void Step(IMovable mover, ref MovementState moveState, float dt)
    {
        // integrate
        moveState.Velocity += moveState.Acceleration * dt;
        moveState.ClampVelocity();

        if (moveState.LinearDamping > 0f)
            moveState.Velocity *= MathF.Max(0, 1 - moveState.LinearDamping * dt);

        moveState.Position += moveState.Velocity * dt;

        // apply; convert if spaces differ
        if (moveState.MovementSpace == mover.PositionSpace)
        {
            mover.SetPosition(moveState.Position);
        }
        
        // when you want to move a pixel-space entity to a grid-space location, e.g. health bar, sprite particle effects, etc.
        else if (moveState.MovementSpace == MovementSpace.Grid && mover.PositionSpace == MovementSpace.Pixel)
        {
            var px = _coords.GetAnchorPixelAtSceneLayerCoordinates(_screenLayer, new PointF(moveState.Position.X, moveState.Position.Y));
            mover.SetPosition(new(px.X, px.Y));
        }
        
        // when you want to move a grid-space entity to a pixel-space location, e.g. player character, pathfinding, etc.
        else
        {
            var gp = _coords.GetSceneLayerCoordinatesAtPixel(_screenLayer, new Point((int)moveState.Position.X, (int)moveState.Position.Y));
            mover.SetPosition(new(gp.X, gp.Y));
        }
    }
}
