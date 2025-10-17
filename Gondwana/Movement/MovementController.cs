using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Scenes;

namespace Gondwana.Movement;

public sealed class MovementController
{
    private readonly ISceneLayerCoordinates? _coords;
    private readonly SceneLayer? _sceneLayer;

    // Pixel-only or Grid-only same-space usage
    private MovementController() { }

    // Layer-aware usage (Grid↔Pixel conversion, wrapping)
    private MovementController(SceneLayer sceneLayer)
    {
        _sceneLayer  = sceneLayer ?? throw new ArgumentNullException(nameof(sceneLayer));
        _coords = _sceneLayer.CoordinateSystem ?? throw new ArgumentNullException(nameof(_sceneLayer.CoordinateSystem));
    }

    public static MovementController ForRenderSurface() => new();
    public static MovementController ForSceneLayer(SceneLayer layer) => new(layer);

    internal void Step(IMovable mover, ref MovementState s, float dt)
    {
        // integrate
        s.Velocity += s.Acceleration * dt;
        s.ClampVelocity();
        
        if (s.LinearDamping > 0f)
            s.Velocity *= MathF.Exp(-s.LinearDamping * dt);  // exp damping

        s.Position += s.Velocity * dt;

        // optional grid wrapping needs coords/layer
        if (s.MovementSpace == CoordinateSpace.Grid && (s.WrapX || s.WrapY))
        {
            if (_coords is null || _sceneLayer is null)
                throw new InvalidOperationException("Grid wrapping requires coordinates/layer.");

            var wrapped = _coords.FindEquivalentSceneLayerCoordinates(
                new PointF(s.Position.X, s.Position.Y),
                _sceneLayer.GridColumnCount - 1, _sceneLayer.GridRowCount - 1);

            s.Position = new Vector2(wrapped.X, wrapped.Y);
        }

        // same-space: no dependencies
        if (s.MovementSpace == mover.PositionSpace)
        {
            mover.SetPosition(s.Position);
            return;
        }

        // cross-space: require coords/layer
        if (_coords is null || _sceneLayer is null)
            throw new InvalidOperationException("Cross-space conversion requires coordinates/layer.");

        if (s.MovementSpace == CoordinateSpace.Grid && mover.PositionSpace == CoordinateSpace.Pixel)
        {
            var px = _coords.GetAnchorPixelAtSceneLayerCoordinates(_sceneLayer,
                      new PointF(s.Position.X, s.Position.Y));
            mover.SetPosition(new Vector2(px.X, px.Y));
            return;
        }

        if (s.MovementSpace == CoordinateSpace.Pixel && mover.PositionSpace == CoordinateSpace.Grid)
        {
            // prefer PointF-taking API if you add one; avoid early truncation
            var gp = _coords.GetSceneLayerCoordinatesAtPixel(_sceneLayer,
                      new Point((int)s.Position.X, (int)s.Position.Y));
            mover.SetPosition(new Vector2(gp.X, gp.Y));
            return;
        }

        throw new InvalidOperationException($"Unsupported conversion {s.MovementSpace}->{mover.PositionSpace}");
    }
}
