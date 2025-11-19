using Gondwana.Movement;
using Gondwana.Scenes;
using System.Drawing;
using System.Numerics;

namespace Gondwana.Rendering;

/// <summary>
/// Represents a world-space camera for a Scene. The camera tracks a
/// world-space upper-left position in pixels and is used by Views to
/// determine which part of the Scene is visible. It supports snapping,
/// smooth follow, dead zones, and clamping to world bounds.
/// </summary>
public sealed class Camera
{
    private readonly Scene _scene;
    private PointF _positionPx = new(0, 0);
    private Func<PointF>? _followWorldPx;
    private bool _hardFollow;

    /// <summary>
    /// Gets or sets the camera's world-space position in pixels, interpreted
    /// as the upper-left corner of the visible region. All world-to-screen
    /// projections for this camera are based on this position.
    /// </summary>
    public PointF PositionPx
    {
        get => _positionPx;
        private set
        {
            if (_positionPx != value)
            {
                _positionPx = value;
                _scene.RefreshNeeded = SceneRefreshType.All;
            }
        }
    }

    /// <summary>
    /// World-space rectangle (in pixels) that the camera is allowed to move within.
    /// Clamping logic uses this to prevent the view from scrolling past the edges
    /// of the world or map.
    /// </summary>
    public RectangleF WorldBoundsPx { get; set; } = RectangleF.Empty;

    /// <summary>
    /// Size of the dead zone, in pixels, around the center of the view where
    /// the follow target is allowed to move without forcing the camera to
    /// track it. A larger dead zone produces a looser, less reactive camera.
    /// </summary>
    public Rectangle DeadZonePx { get; set; } = Rectangle.Empty;

    /// <summary>
    /// Controls how quickly the camera moves toward its follow target when
    /// smooth follow is enabled. Interpreted as a lerp factor per second:
    /// higher values feel snappier, lower values feel more floaty.
    /// </summary>
    public float FollowLerpPerSecond { get; set; } = 8f;

    internal Camera(Scene scene) => _scene = scene ?? throw new ArgumentNullException(nameof(scene));

    /// <summary>
    /// Returns the current visible world size (in pixels).
    /// Typically assigned by <see cref="View"/> to point at its <see cref="Viewport.VisibleWorldSizePx"/>.
    /// </summary>
    internal Func<SizeF> GetVisibleWorldSizePx { get; set; } = () => new SizeF(1280, 720);

    #region Camera movement methods

    /// <summary>
    /// Instantly moves the camera to the specified world-space position,
    /// interpreted as the upper-left corner of the visible region. Any
    /// active smooth follow continues from this new position.
    /// </summary>
    /// <param name="worldUpperLeftPx">
    /// World-space pixel position for the camera's upper-left corner.
    /// </param>
    public void SnapTo(PointF worldUpperLeftPx)
    {
        PositionPx = ClampToWorldBounds(worldUpperLeftPx);
    }

    /// <summary>
    /// Instantly repositions the camera so that the given world-space point
    /// appears at the center of the view. Uses the current visible world
    /// size to compute the appropriate camera upper-left position.
    /// </summary>
    /// <param name="worldCenterPx">
    /// World-space pixel position that should be centered on screen.
    /// </param>
    public void CenterOn(PointF worldCenterPx)
    {
        var vis = GetVisibleWorldSizePx();
        SnapTo(new PointF(worldCenterPx.X - vis.Width * 0.5f,
                          worldCenterPx.Y - vis.Height * 0.5f));
    }

    /// <summary>
    /// Instantly centers the camera on the specified tile in the given
    /// <see cref="SceneLayer"/>. The tile's visual center is placed at the
    /// center of the view.
    /// </summary>
    /// <param name="layer">
    /// The SceneLayer that owns the grid coordinate.
    /// </param>
    /// <param name="col">
    /// Tile column in grid coordinates.
    /// </param>
    /// <param name="row">
    /// Tile row in grid coordinates.
    /// </param>
    public void CenterOnGrid(SceneLayer layer, int col, int row)
    {
        // Anchor (top-left of tile)
        var anchor = layer.GridToWorldPx(new PointF(col, row));
        float tileCenterX = anchor.X + layer.SceneLayerTileWidth * 0.5f;
        float tileCenterY = anchor.Y + layer.SceneLayerTileHeight * 0.5f;

        var vis = GetVisibleWorldSizePx();

        // Camera is top-left, so subtract half the visible size to center that point.
        var camTopLeft = new PointF(
            tileCenterX - vis.Width * 0.5f,
            tileCenterY - vis.Height * 0.5f);

        SnapTo(camTopLeft);
    }

    /// <summary>
    /// Smoothly pans the camera so that the given world-space point ends up at
    /// the center of the visible view, using the given follow speed.
    /// </summary>
    /// <param name="worldCenterPx">
    /// World-space pixel position that should appear in the center of the view.
    /// </param>
    /// <param name="speed">
    /// Follow speed in lerp-units per second. Higher values feel snappier,
    /// lower values feel more floaty.
    /// </param>
    public void PanCenterTo(PointF worldCenterPx, float speed)
    {
        FollowLerpPerSecond = speed;

        var vis = GetVisibleWorldSizePx();
        var camTargetTopLeft = new PointF(
            worldCenterPx.X - vis.Width * 0.5f,
            worldCenterPx.Y - vis.Height * 0.5f);

        FollowTo(camTargetTopLeft);
    }

    /// <summary>
    /// Smoothly pans the camera until the specified grid tile is centered in
    /// the view. Uses the given follow speed for the motion.
    /// </summary>
    /// <param name="layer">
    /// The SceneLayer that owns the grid coordinate.
    /// </param>
    /// <param name="col">
    /// Tile column in grid coordinates.
    /// </param>
    /// <param name="row">
    /// Tile row in grid coordinates.
    /// </param>
    /// <param name="speed">
    /// Follow speed in lerp-units per second used for the pan.
    /// </param>
    public void AnimateCenterOnGrid(SceneLayer layer, int col, int row, float speed)
    {
        // Reuse the same center calculation as CenterOnGrid
        var anchor = layer.GridToWorldPx(new PointF(col, row));
        float tileCenterX = anchor.X + layer.SceneLayerTileWidth * 0.5f;
        float tileCenterY = anchor.Y + layer.SceneLayerTileHeight * 0.5f;

        PanCenterTo(new PointF(tileCenterX, tileCenterY), speed);
    }

    /// <summary>
    /// Convenience helper that smoothly pans the camera to center on a
    /// specific grid tile in the given SceneLayer.
    /// </summary>
    /// <param name="layer">
    /// The SceneLayer that owns the grid coordinate.
    /// </param>
    /// <param name="col">
    /// Tile column in grid coordinates.
    /// </param>
    /// <param name="row">
    /// Tile row in grid coordinates.
    /// </param>
    /// <param name="speed">
    /// Follow speed in lerp-units per second used for the pan.
    /// </param>
    public void PanToGrid(SceneLayer layer, int col, int row, float speed)
    {
        AnimateCenterOnGrid(layer, col, row, speed);
    }

    /// <summary>
    /// Smoothly pans the camera toward a world-space top-left position for the
    /// view, using the given follow speed.
    /// </summary>
    /// <param name="worldUpperLeftPx">
    /// World-space pixel position for the camera's upper-left corner.
    /// </param>
    /// <param name="speed">
    /// Follow speed in lerp-units per second. Higher values feel snappier,
    /// lower values feel more floaty.
    /// </param>
    public void PanTo(PointF worldTopLeftPx, float speed)
    {
        FollowLerpPerSecond = speed;
        FollowTo(worldTopLeftPx);
    }

    /// <summary>
    /// Instantly pans the camera by the given offset in world-space pixels.
    /// This adds the offset to the current camera position without changing
    /// any follow targets.
    /// </summary>
    /// <param name="dx">
    /// Horizontal offset in world-space pixels (positive moves right).
    /// </param>
    /// <param name="dy">
    /// Vertical offset in world-space pixels (positive moves down).
    /// </param>
    public void PanBy(PointF deltaPx) => SnapTo(new PointF(PositionPx.X + deltaPx.X, PositionPx.Y + deltaPx.Y));

    /// <summary>
    /// Configures the camera to follow a dynamically supplied world-space
    /// target position. Each update, the supplier is called to get the
    /// desired camera upper-left in world pixels, and the camera moves
    /// toward it using smooth or hard follow.
    /// </summary>
    /// <param name="followTargetSupplier">
    /// Function that returns the desired camera upper-left position in
    /// world-space pixels each frame.
    /// </param>
    /// <param name="hard">
    /// If true, the camera snaps directly to the supplied position (no
    /// smoothing). If false, the camera smoothly lerps toward the target
    /// using <see cref="FollowLerpPerSecond"/>.
    /// </param>
    public void Follow(Func<PointF> getWorldPixel, bool hardFollow = false)
    {
        _followWorldPx = getWorldPixel ?? throw new ArgumentNullException(nameof(getWorldPixel));
        _hardFollow = hardFollow;
    }

    /// <summary>
    /// Starts smooth camera movement toward a fixed world-space top-left position.
    /// Uses the same follow logic as <see cref="Follow(Func{PointF}, bool)"/> but
    /// with a constant target instead of a dynamic supplier.
    /// </summary>
    /// <param name="worldUpperLeftPx">
    /// World-space pixel position for the camera's upper-left corner.
    /// </param>
    /// <param name="hard">
    /// If true, the camera snaps directly to the target each frame (hard follow).
    /// If false, movement is smoothed using <see cref="FollowLerpPerSecond"/>.
    /// </param>
    public void FollowTo(PointF worldTopLeftPx)
    {
        // Freeze the value so the camera lerps toward a fixed point.
        Follow(() => worldTopLeftPx);
    }

    /// <summary>
    /// Makes the camera follow an IMovable-on-SceneLayer so that the object
    /// stays centered in the view. Supports both grid-space and pixel-space
    /// movement, using the current coordinate system for the layer.
    /// </summary>
    /// <param name="target">
    /// The movable object to follow. Its <see cref="IMovableOnSceneLayer.Position"/>
    /// and <see cref="IMovableOnSceneLayer.PositionSpace"/> are used to determine
    /// the world-space center point.
    /// </param>
    /// <param name="speed">
    /// Optional follow speed in lerp-units per second. If &lt;= 0, the existing
    /// <see cref="FollowLerpPerSecond"/> value is used.
    /// </param>
    /// <param name="hard">
    /// If true, uses hard follow (no smoothing). If false, uses smooth follow
    /// based on <see cref="FollowLerpPerSecond"/>.
    /// </param>
    public void FollowCentered(IMovableOnSceneLayer target, float speed = -1f, bool hard = false)
    {
        if (speed > 0f)
            FollowLerpPerSecond = speed;

        Follow(() =>
        {
            var layer = target.SceneLayer;
            var pos = target.GetPosition();     // Vector2

            // Convert to world center px
            PointF worldCenter =
                target.PositionSpace == MovementSpace.Grid
                    ? GetCenteredTile(layer, pos)
                    : new PointF(pos.X, pos.Y);

            // Convert center → camera UL
            var vis = GetVisibleWorldSizePx();
            return new PointF(
                worldCenter.X - vis.Width * 0.5f,
                worldCenter.Y - vis.Height * 0.5f);
        },
        hard);
    }

    /// <summary>
    /// Smoothly follows an IMovable target, centering it horizontally only.
    /// Vertical camera position is left unchanged.
    /// </summary>
    /// <param name="target">
    /// The movable object to follow. Its <see cref="IMovableOnSceneLayer.Position"/>
    /// and <see cref="IMovableOnSceneLayer.PositionSpace"/> are used to determine
    /// the world-space center point.
    /// </param>
    /// <param name="speed">
    /// Optional follow speed in lerp-units per second. If &lt;= 0, the existing
    /// <see cref="FollowLerpPerSecond"/> value is used.
    /// </param>
    /// <param name="hard">
    /// If true, uses hard follow (no smoothing). If false, uses smooth follow
    /// based on <see cref="FollowLerpPerSecond"/>.
    /// </param>
    public void FollowCenteredX(IMovableOnSceneLayer target, float speed = -1f, bool hard = false)
    {
        if (speed > 0f)
            FollowLerpPerSecond = speed;

        Follow(() =>
        {
            var layer = target.SceneLayer;
            var pos = target.GetPosition();

            // Convert to world pixel center
            float worldCenterX;
            if (target.PositionSpace == MovementSpace.Grid)
            {
                var anchor = layer.GridToWorldPx(new PointF(pos.X, pos.Y));
                worldCenterX = anchor.X + layer.SceneLayerTileWidth * 0.5f;
            }
            else
            {
                worldCenterX = pos.X;
            }

            // Current camera Y stays unchanged
            float currentCamY = PositionPx.Y;
            var vis = GetVisibleWorldSizePx();

            return new PointF(
                worldCenterX - vis.Width * 0.5f,
                currentCamY);
        },
        hard);
    }


    /// <summary>
    /// Smoothly follows an IMovable target, centering it vertically only.
    /// Horizontal camera position is left unchanged.
    /// </summary>
    /// <param name="target">
    /// The movable object to follow. Its <see cref="IMovableOnSceneLayer.Position"/>
    /// and <see cref="IMovableOnSceneLayer.PositionSpace"/> are used to determine
    /// the world-space center point.
    /// </param>
    /// <param name="speed">
    /// Optional follow speed in lerp-units per second. If &lt;= 0, the existing
    /// <see cref="FollowLerpPerSecond"/> value is used.
    /// </param>
    /// <param name="hard">
    /// If true, uses hard follow (no smoothing). If false, uses smooth follow
    /// based on <see cref="FollowLerpPerSecond"/>.
    /// </param>
    public void FollowCenteredY(IMovableOnSceneLayer target, float speed = -1f, bool hard = false)
    {
        if (speed > 0f)
            FollowLerpPerSecond = speed;

        Follow(() =>
        {
            var layer = target.SceneLayer;
            var pos = target.GetPosition();

            // Convert to world pixel center
            float worldCenterY;
            if (target.PositionSpace == MovementSpace.Grid)
            {
                var anchor = layer.GridToWorldPx(new PointF(pos.X, pos.Y));
                worldCenterY = anchor.Y + layer.SceneLayerTileHeight * 0.5f;
            }
            else
            {
                worldCenterY = pos.Y;
            }

            // Current camera X stays unchanged
            float currentCamX = PositionPx.X;
            var vis = GetVisibleWorldSizePx();

            return new PointF(
                currentCamX,
                worldCenterY - vis.Height * 0.5f);
        },
        hard);
    }

    public void ClearFollow() => _followWorldPx = null;

    #endregion Camera movement methods

    internal void Update(float dtSeconds)
    {
        if (_followWorldPx is null)
            return;

        var desiredUL = DesiredUpperLeftToContainTarget(_followWorldPx());
        if (_hardFollow || FollowLerpPerSecond <= 0f)
        {
            PositionPx = ClampToWorldBounds(desiredUL);
        }
        else
        {
            float t = 1f - (float)Math.Exp(-FollowLerpPerSecond * Math.Max(0f, dtSeconds));
            var clamped = ClampToWorldBounds(desiredUL);
            PositionPx = new PointF(PositionPx.X + (clamped.X - PositionPx.X) * t,
                                    PositionPx.Y + (clamped.Y - PositionPx.Y) * t);
        }
    }

    #region private methods
    
    private PointF DesiredUpperLeftToContainTarget(PointF targetWorldPx)
    {
        var vis = GetVisibleWorldSizePx();
        if (DeadZonePx == Rectangle.Empty)
            return new PointF(targetWorldPx.X - vis.Width * 0.5f,
                              targetWorldPx.Y - vis.Height * 0.5f);

        var viewWorld = new RectangleF(PositionPx.X, PositionPx.Y, vis.Width, vis.Height);
        var dzWorld = new RectangleF(viewWorld.X + DeadZonePx.X,
                                     viewWorld.Y + DeadZonePx.Y,
                                     DeadZonePx.Width, DeadZonePx.Height);

        if (dzWorld.Contains(targetWorldPx))
            return PositionPx;

        float newX = PositionPx.X;
        float newY = PositionPx.Y;

        if (targetWorldPx.X < dzWorld.Left)
            newX -= (dzWorld.Left - targetWorldPx.X);

        if (targetWorldPx.X > dzWorld.Right)
            newX += (targetWorldPx.X - dzWorld.Right);

        if (targetWorldPx.Y < dzWorld.Top)
            newY -= (dzWorld.Top - targetWorldPx.Y);

        if (targetWorldPx.Y > dzWorld.Bottom)
            newY += (targetWorldPx.Y - dzWorld.Bottom);

        return new PointF(newX, newY);
    }

    private PointF ClampToWorldBounds(PointF ul)
    {
        if (WorldBoundsPx == RectangleF.Empty)
            return ul;

        var vis = GetVisibleWorldSizePx();
        float minX = WorldBoundsPx.Left;
        float minY = WorldBoundsPx.Top;
        float maxX = WorldBoundsPx.Right - vis.Width;
        float maxY = WorldBoundsPx.Bottom - vis.Height;

        if (maxX < minX)
            maxX = minX;

        if (maxY < minY)
            maxY = minY;

        return new PointF(Math.Clamp(ul.X, minX, maxX),
                          Math.Clamp(ul.Y, minY, maxY));
    }

    /// <summary>
    /// Computes the world-space center point of a tile at the given grid
    /// position within a SceneLayer.
    /// </summary>
    /// <param name="layer">
    /// The SceneLayer that owns the grid coordinate.
    /// </param>
    /// <param name="gridPos">
    /// Grid position (col,row) as a Vector2.
    /// </param>
    /// <returns>
    /// World-space pixel position at the visual center of the tile.
    /// </returns>
    private static PointF GetCenteredTile(SceneLayer layer, Vector2 gridPos)
    {
        var anchor = layer.GridToWorldPx(new PointF(gridPos.X, gridPos.Y));
        return new PointF(
            anchor.X + layer.SceneLayerTileWidth * 0.5f,
            anchor.Y + layer.SceneLayerTileHeight * 0.5f);
    }

    #endregion private methods
}
