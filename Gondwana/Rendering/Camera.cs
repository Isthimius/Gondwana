using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Coordinates;
using Gondwana.Movement;
using Gondwana.Scenes;
using Microsoft.Extensions.Logging;

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

    // Explicit pan-to-target (camera upper-left) state.
    // This is used by PanTo and is independent of the "follow" target.
    private PointF? _panTargetUpperLeftPx;
    private float _panLerpPerSecond;

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

                if (_scene is not null)
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
    /// active smooth follow or pan continues from this new position.
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

        // For center-based pans, we treat the target as a "point of interest"
        // and let the follow/dead-zone logic convert it to a camera UL.
        Follow(() => worldCenterPx, hardFollow: false);
    }

    /// <summary>
    /// Smoothly pans the camera until the specified grid tile is centered in
    /// the view. Uses the given follow speed for the motion.
    /// </summary>
    public void AnimateCenterOnGrid(SceneLayer layer, int col, int row, float speed)
    {
        // Compute world center of the tile and reuse PanCenterTo.
        var anchor = layer.GridToWorldPx(new PointF(col, row));
        float tileCenterX = anchor.X + layer.SceneLayerTileWidth * 0.5f;
        float tileCenterY = anchor.Y + layer.SceneLayerTileHeight * 0.5f;

        PanCenterTo(new PointF(tileCenterX, tileCenterY), speed);
    }

    /// <summary>
    /// Smoothly pans the camera toward a world-space top-left position for the
    /// view, using the given follow speed. This interprets the input as the
    /// desired camera upper-left, not a center point.
    /// </summary>
    /// <param name="worldTopLeftPx">
    /// World-space pixel position for the camera's upper-left corner.
    /// </param>
    /// <param name="speed">
    /// Pan speed in lerp-units per second. Higher values feel snappier,
    /// lower values feel more floaty. If &lt;= 0, the camera snaps.
    /// </param>
    public void PanTo(PointF worldTopLeftPx, float speed)
    {
        // Cancel any center-based follow when we take direct manual control.
        _followWorldPx = null;
        _hardFollow = false;

        if (speed <= 0f)
        {
            _panTargetUpperLeftPx = null;
            SnapTo(worldTopLeftPx);
            return;
        }

        _panLerpPerSecond = speed;
        _panTargetUpperLeftPx = ClampToWorldBounds(worldTopLeftPx);
    }

    /// <summary>
    /// Smoothly pans the camera so that the specified world-space point becomes
    /// the visual center of the view, then stops. Unlike <see cref="PanCenterTo"/>,
    /// this is a one-shot cinematic pan and does not continue tracking the point
    /// after the camera arrives.
    /// </summary>
    /// <param name="worldCenterPx">
    /// World-space pixel position that should end up at the center of the view.
    /// </param>
    /// <param name="speed">
    /// Pan speed in lerp-units per second. Higher values feel snappier,
    /// lower values feel more floaty. If &lt;= 0, the camera snaps immediately
    /// to the target center.
    /// </param>
    public void PanCenterToOnce(PointF worldCenterPx, float speed)
    {
        // Compute the desired camera upper-left such that worldCenterPx
        // ends up in the middle of the visible region.
        var vis = GetVisibleWorldSizePx();

        var desiredUpperLeft = new PointF(
            worldCenterPx.X - vis.Width * 0.5f,
            worldCenterPx.Y - vis.Height * 0.5f);

        // Delegate to the existing pan-to-upper-left logic (which also clamps).
        PanTo(desiredUpperLeft, speed);
    }

    /// <summary>
    /// Smoothly pans the camera so that its upper-left pixel reaches the specified
    /// world-space target over approximately the given duration. This uses the same
    /// exponential smoothing model as <see cref="PanTo"/>, but computes an appropriate
    /// lerp rate so the camera covers ~99% of the distance in the requested time.
    /// </summary>
    /// <param name="worldTopLeftPx">
    /// Desired world-space pixel position for the camera's upper-left corner.
    /// </param>
    /// <param name="durationSeconds">
    /// Approximate time (in seconds) for the camera to reach the target.
    /// Values &lt;= 0 cause an immediate snap to the destination.
    /// </param>
    public void PanToOverDuration(PointF worldTopLeftPx, float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            // Degenerate case — behave like SnapTo().
            _panTargetUpperLeftPx = null;
            SnapTo(worldTopLeftPx);
            return;
        }

        // In an exponential smoothing system:
        //      pos(t) = target + (pos0 - target) * exp(-k * t)
        //
        // To reach ~99% of target position in T seconds:
        //      exp(-k*T) = 0.01   →   k = -ln(0.01) / T
        //
        // This produces a visually nice, predictable time-to-arrive.
        float k = -(float)Math.Log(0.01f) / durationSeconds;

        // Cancel following and start a timed pan using your existing machinery.
        _followWorldPx = null;
        _hardFollow = false;

        _panLerpPerSecond = k;
        _panTargetUpperLeftPx = ClampToWorldBounds(worldTopLeftPx);
    }

    /// <summary>
    /// Smoothly pans the camera so that the specified world-space point becomes
    /// the visual center of the view over approximately the given duration,
    /// then stops. Unlike <see cref="PanCenterTo"/>, this is a one-shot
    /// cinematic pan and does not continue tracking the point afterward.
    /// </summary>
    /// <param name="worldCenterPx">
    /// World-space pixel position that should end up at the center of the view.
    /// </param>
    /// <param name="durationSeconds">
    /// Approximate time (in seconds) for the camera to reach the target center.
    /// Values &lt;= 0 cause an immediate snap to the destination.
    /// </param>
    public void PanCenterToOverDuration(PointF worldCenterPx, float durationSeconds)
    {
        // Compute the desired camera upper-left such that worldCenterPx
        // ends up in the middle of the visible region.
        var vis = GetVisibleWorldSizePx();

        var desiredUpperLeft = new PointF(
            worldCenterPx.X - vis.Width * 0.5f,
            worldCenterPx.Y - vis.Height * 0.5f);

        // Delegate to the duration-based UL pan logic.
        PanToOverDuration(desiredUpperLeft, durationSeconds);
    }

    /// <summary>
    /// Smoothly pans the camera so that the specified grid tile becomes the
    /// visual center of the view over approximately the given duration.
    /// This is a one-shot cinematic pan to a tile, not a continuous follow.
    /// </summary>
    /// <param name="layer">
    /// Scene layer the grid position belongs to.
    /// </param>
    /// <param name="col">Grid column of the tile to center on.</param>
    /// <param name="row">Grid row of the tile to center on.</param>
    /// <param name="durationSeconds">
    /// Approximate time (in seconds) for the camera to reach the target cell.
    /// Values &lt;= 0 cause an immediate snap to the destination.
    /// </param>
    public void PanToGridOverDuration(SceneLayer layer, int col, int row, float durationSeconds)
    {
        if (layer is null) throw new ArgumentNullException(nameof(layer));

        // Same pattern as AnimateCenterOnGrid: get the tile's world-space anchor
        // (top-left), then offset by half the tile size to get its visual center.
        var anchor = layer.GridToWorldPx(new PointF(col, row));
        float tileCenterX = anchor.X + layer.SceneLayerTileWidth * 0.5f;
        float tileCenterY = anchor.Y + layer.SceneLayerTileHeight * 0.5f;

        // Then pan so that tile center ends up at the center of the view
        // over the requested duration.
        PanCenterToOverDuration(new PointF(tileCenterX, tileCenterY), durationSeconds);
    }

    /// <summary>
    /// Instantly pans the camera by the given offset in world-space pixels.
    /// This adds the offset to the current camera position without changing
    /// any follow or pan targets.
    /// </summary>
    public void PanBy(PointF deltaPx)
    {
        SnapTo(new PointF(PositionPx.X + deltaPx.X,
                          PositionPx.Y + deltaPx.Y));
    }

    /// <summary>
    /// Configures the camera to follow a dynamically supplied world-space
    /// target position. Each update, the supplier is called to get the
    /// desired target "point of interest" in world pixels (typically a
    /// character center), and the camera moves so that point stays visible,
    /// honoring dead-zones and clamping.
    /// </summary>
    /// <param name="getWorldPixel">
    /// Function that returns the target world-space point of interest each frame.
    /// </param>
    /// <param name="hardFollow">
    /// If true, the camera snaps directly to the desired position (no smoothing).
    /// If false, the camera smoothly lerps toward the target using
    /// <see cref="FollowLerpPerSecond"/>.
    /// </param>
    public void Follow(Func<PointF> getWorldPixel, bool hardFollow = false)
    {
        _followWorldPx = getWorldPixel ?? throw new ArgumentNullException(nameof(getWorldPixel));
        _hardFollow = hardFollow;
    }

    /// <summary>
    /// Starts smooth camera movement toward a fixed world-space "point of
    /// interest" (typically a center point). Uses the same follow logic as
    /// <see cref="Follow(Func{PointF}, bool)"/> but with a constant target.
    /// </summary>
    /// <param name="worldPointOfInterestPx">
    /// World-space pixel position of the target point of interest.
    /// </param>
    public void FollowTo(PointF worldPointOfInterestPx)
    {
        // Freeze the value so the camera lerps toward a fixed point.
        Follow(() => worldPointOfInterestPx);
    }

    /// <summary>
    /// Makes the camera follow an IMovable-on-SceneLayer so that the object
    /// stays centered in the view. Supports both grid-space and pixel-space
    /// movement, using the current coordinate system for the layer.
    /// </summary>
    public void FollowCentered(IMovableOnSceneLayer target, float speed = -1f, bool hard = false)
    {
        if (speed > 0f)
            FollowLerpPerSecond = speed;

        Follow(() =>
        {
            var layer = target.SceneLayer;
            var pos = target.GetPosition();     // Vector2

            // Convert to world-space center.
            PointF worldCenter =
                target.PositionSpace == MovementSpace.Grid
                    ? GetCenteredTile(layer, pos)
                    : new PointF(pos.X, pos.Y);

            return worldCenter; // treated as point-of-interest (center)
        },
        hard);
    }

    /// <summary>
    /// Smoothly follows an IMovable target, centering it horizontally only.
    /// Vertical camera position is left unchanged.
    /// </summary>
    public void FollowCenteredX(IMovableOnSceneLayer target, float speed = -1f, bool hard = false)
    {
        if (speed > 0f)
            FollowLerpPerSecond = speed;

        Follow(() =>
        {
            var layer = target.SceneLayer;
            var pos = target.GetPosition();

            // Convert to world pixel center X.
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

            // Use current camera Y as the vertical "anchor".
            float currentCamY = PositionPx.Y;
            var vis = GetVisibleWorldSizePx();

            // Reconstruct a center point whose Y keeps the current camera row.
            return new PointF(
                worldCenterX,
                currentCamY + vis.Height * 0.5f);
        },
        hard);
    }

    /// <summary>
    /// Smoothly follows an IMovable target, centering it vertically only.
    /// Horizontal camera position is left unchanged.
    /// </summary>
    public void FollowCenteredY(IMovableOnSceneLayer target, float speed = -1f, bool hard = false)
    {
        if (speed > 0f)
            FollowLerpPerSecond = speed;

        Follow(() =>
        {
            var layer = target.SceneLayer;
            var pos = target.GetPosition();

            // Convert to world pixel center Y.
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

            // Use current camera X as the horizontal "anchor".
            float currentCamX = PositionPx.X;
            var vis = GetVisibleWorldSizePx();

            // Reconstruct a center point whose X keeps the current camera column.
            return new PointF(
                currentCamX + vis.Width * 0.5f,
                worldCenterY);
        },
        hard);
    }

    /// <summary>
    /// Clears any active follow or pan targets; the camera remains at its
    /// current position until moved again.
    /// </summary>
    public void ClearFollow()
    {
        _followWorldPx = null;
        _panTargetUpperLeftPx = null;
        _hardFollow = false;
    }

    #endregion Camera movement methods

    internal void Update(float dtSeconds)
    {
        // 1) Explicit pan-to-UL (PanTo) takes priority over center-follow.
        if (_panTargetUpperLeftPx is { } panTarget)
        {
            var clamped = ClampToWorldBounds(panTarget);

            if (_hardFollow || _panLerpPerSecond <= 0f)
            {
                PositionPx = clamped;
                _panTargetUpperLeftPx = null;
            }
            else
            {
                float t = 1f - (float)Math.Exp(-_panLerpPerSecond * Math.Max(0f, dtSeconds));
                var newPos = new PointF(
                    PositionPx.X + (clamped.X - PositionPx.X) * t,
                    PositionPx.Y + (clamped.Y - PositionPx.Y) * t);

                PositionPx = newPos;

                // Close enough → snap and finish.
                if (Math.Abs(newPos.X - clamped.X) < 0.5f &&
                    Math.Abs(newPos.Y - clamped.Y) < 0.5f)
                {
                    PositionPx = clamped;
                    _panTargetUpperLeftPx = null;
                }
            }

            // If we're actively panning, don't also run center-follow this frame.
            if (_panTargetUpperLeftPx is not null)
                return;
        }

        // 2) Center-follow / dead-zone logic.
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
            PositionPx = new PointF(
                PositionPx.X + (clamped.X - PositionPx.X) * t,
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

        return new PointF(
            Math.Clamp(ul.X, minX, maxX),
            Math.Clamp(ul.Y, minY, maxY));
    }

    /// <summary>
    /// Computes the world-space center point of a tile at the given grid
    /// position within a SceneLayer.
    /// </summary>
    private static PointF GetCenteredTile(SceneLayer layer, Vector2 gridPos)
    {
        var anchor = layer.GridToWorldPx(new PointF(gridPos.X, gridPos.Y));
        return new PointF(
            anchor.X + layer.SceneLayerTileWidth * 0.5f,
            anchor.Y + layer.SceneLayerTileHeight * 0.5f);
    }

    #endregion private methods
}
