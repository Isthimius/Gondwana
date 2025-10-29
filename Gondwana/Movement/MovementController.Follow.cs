using Gondwana.Movement.Easing;
using Gondwana.Scenes;
using System.Drawing;
using System.Numerics;

namespace Gondwana.Movement;

public sealed partial class MovementController
{
    // --- Pixel-follow (DirectDrawing → DirectDrawing)
    private Func<Vector2>? _followPixel;        // returns current target pixel position each frame
    private Vector2 _followOffsetPx;
    private bool _followHard;
    private float _followSpeedPxPerSec;
    private float _followSnapPx;

    // Grid-follow (Scene-layer → this DirectDrawing in pixels)
    private IMovableOnSceneLayer? _followGridTarget;
    private Vector2 _followGridOffset;
    private float _followSpeedTilesPerSec;
    private float _followSnapTiles;

    // Easing follow
    private Func<float, float>? _followEasing;  // optional easing curve
    private float _followDurationSec;           // duration for easing-based follow

    /// <summary>
    /// Start following a <b>pixel-space target</b> at a constant speed.
    /// The <paramref name="speed"/> and <paramref name="snap"/> units are interpreted
    /// in the <i>follower's</i> position space:
    /// <list type="bullet">
    /// <item><description>Pixel follower → pixels/sec and pixels</description></item>
    /// <item><description>Grid follower → tiles/sec and tiles</description></item>
    /// </list>
    /// </summary>
    /// <param name="getPixelPos">Delegate that returns the current target position in pixels (screen/world pixels) each frame.</param>
    /// <param name="speed">Follow speed (pixels/sec for pixel followers; tiles/sec for grid followers). Must be &gt; 0.</param>
    /// <param name="snap">Snap/arrival tolerance (pixels for pixel followers; tiles for grid followers). Values &lt; 0 are clamped to 0.</param>
    /// <param name="offsetPx">Optional additional pixel offset applied only when the follower is in pixel space (ignored for grid followers).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="getPixelPos"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="speed"/> ≤ 0.</exception>
    public void FollowPixelSoft(Func<Vector2> getPixelPos,
                                float speed,
                                float snap = 0.5f,
                                Vector2 offsetPx = default)
    {
        if (speed <= 0)
            throw new ArgumentOutOfRangeException(nameof(speed));

        _followPixel = getPixelPos ?? throw new ArgumentNullException(nameof(getPixelPos));
        _followGridTarget = null;
        _followHard = false;

        if (_mover.PositionSpace == CoordinateSpace.Pixel)
        {
            _followOffsetPx = offsetPx;
            _followSpeedPxPerSec = speed;
            _followSnapPx = MathF.Max(0f, snap);
            _followSpeedTilesPerSec = 0f;
            _followSnapTiles = 0f;
        }
        else
        {
            // grid follower chasing a pixel target: interpret speed/snap as tiles-based
            _followOffsetPx = Vector2.Zero;   // pixel offset not used for grid follower
            _followSpeedTilesPerSec = speed;
            _followSnapTiles = MathF.Max(0f, snap);
            _followSpeedPxPerSec = 0f;
            _followSnapPx = 0f;
        }

        // clear tween state
        _followEasing = null;
        _followDurationSec = 0f;
    }

    /// <summary>
    /// Start following a <b>pixel-space target</b> using a duration-based tween (easing).
    /// When easing is active, speed fields are ignored. The <paramref name="snapPx"/> value
    /// is interpreted in the follower's space at runtime:
    /// <list type="bullet">
    /// <item><description>Pixel follower uses <paramref name="snapPx"/> as pixels.</description></item>
    /// <item><description>Grid follower treats <paramref name="snapPx"/> as tiles (internally mapped).</description></item>
    /// </list>
    /// </summary>
    /// <param name="getPixelPos">Delegate that returns the current target position in pixels each frame.</param>
    /// <param name="durationSec">Tween duration in seconds. Must be &gt; 0.</param>
    /// <param name="easing">Optional easing function in the range [0,1]→[0,1]. If null, linear easing is used.</param>
    /// <param name="snapPx">Arrival tolerance; interpreted in the follower's space at runtime (pixels for pixel followers, tiles for grid followers).</param>
    /// <param name="offsetPx">Optional additional pixel offset when the follower is pixel-space (ignored for grid followers).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="getPixelPos"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="durationSec"/> ≤ 0.</exception>
    public void FollowPixelSoft(Func<Vector2> getPixelPos,
                                float durationSec,
                                Func<float, float>? easing = null,
                                float snapPx = 0.5f,
                                Vector2 offsetPx = default)
    {
        if (durationSec <= 0f)
            throw new ArgumentOutOfRangeException(nameof(durationSec));

        _followPixel = getPixelPos ?? throw new ArgumentNullException(nameof(getPixelPos));
        _followGridTarget = null;
        _followHard = false;

        _followOffsetPx = offsetPx;         // only applied for pixel followers
        _followEasing = easing ?? EasingFunctions.Linear;
        _followDurationSec = durationSec;

        // Interpret snap in the follower’s space
        if (_mover.PositionSpace == CoordinateSpace.Pixel)
        {
            _followSnapPx = MathF.Max(0f, snapPx);
            _followSnapTiles = 0f;
        }
        else
        {
            _followSnapTiles = MathF.Max(0f, snapPx);
            _followSnapPx = 0f;
        }

        // neutralize speed fields
        _followSpeedPxPerSec = 0f;
        _followSpeedTilesPerSec = 0f;
    }

    /// <summary>
    /// Convenience overload of <see cref="FollowPixelSoft(Func{Vector2}, float, Func{float, float}?, float, Vector2)"/>
    /// that specifies the easing curve via <see cref="EasingKind"/>.
    /// </summary>
    /// <param name="getPixelPos">Delegate that returns the current target position in pixels each frame.</param>
    /// <param name="durationSec">Tween duration in seconds. Must be &gt; 0.</param>
    /// <param name="easingKind">Named easing preset.</param>
    /// <param name="snapPx">Arrival tolerance; interpreted in the follower's space at runtime.</param>
    /// <param name="offsetPx">Optional additional pixel offset for pixel-space followers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="getPixelPos"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="durationSec"/> ≤ 0.</exception>
    public void FollowPixelSoft(Func<Vector2> getPixelPos,
                                float durationSec,
                                EasingKind easingKind,
                                float snapPx = 0.5f,
                                Vector2 offsetPx = default)
    {
        var easing = EasingFunctions.From(easingKind);
        FollowPixelSoft(getPixelPos, durationSec, easing, snapPx, offsetPx);
    }

    /// <summary>
    /// Hard-follow a <b>pixel-space target</b>. The follower snaps directly to the target
    /// every update (no tweening, no speed). An optional pixel offset is applied only for pixel-space followers.
    /// </summary>
    /// <param name="getPixelPos">Delegate that returns the current target position in pixels each frame.</param>
    /// <param name="offsetPx">Optional additional pixel offset applied only when the follower is in pixel space.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="getPixelPos"/> is null.</exception>
    public void FollowPixelHard(Func<Vector2> getPixelPos, Vector2 offsetPx = default)
    {
        _followPixel = getPixelPos ?? throw new ArgumentNullException(nameof(getPixelPos));
        _followGridTarget = null;
        _followHard = true;

        _followOffsetPx = (_mover.PositionSpace == CoordinateSpace.Pixel) ? offsetPx : Vector2.Zero;

        // neutralize speed/tween fields
        _followSpeedPxPerSec = 0f;
        _followSnapPx = 0f;
        _followSpeedTilesPerSec = 0f;
        _followSnapTiles = 0f;
        _followEasing = null;
        _followDurationSec = 0f;
    }

    /// <summary>
    /// Start following a <b>grid (tile) target</b> at a constant speed (tiles/sec).
    /// If the follower is pixel-space, an additional pixel offset can be applied after the grid→pixel conversion.
    /// </summary>
    /// <param name="tileTarget">The grid-anchored target to follow (must expose <see cref="SceneLayer"/> and grid position).</param>
    /// <param name="speedTilesPerSec">Follow speed in tiles per second. Must be &gt; 0.</param>
    /// <param name="snapTiles">Tile-space arrival tolerance; values &lt; 0 are clamped to 0.</param>
    /// <param name="gridOffset">Optional tile offset applied to the target's grid coordinate before conversion.</param>
    /// <param name="pixelOffset">Optional pixel offset applied only when the follower is pixel-space.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tileTarget"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="speedTilesPerSec"/> ≤ 0.</exception>
    public void FollowTileSoft(IMovableOnSceneLayer tileTarget,
                               float speedTilesPerSec,
                               float snapTiles = 0.25f,
                               Vector2 gridOffset = default,
                               Vector2 pixelOffset = default)
    {
        if (speedTilesPerSec <= 0)
            throw new ArgumentOutOfRangeException(nameof(speedTilesPerSec));

        _followGridTarget = tileTarget ?? throw new ArgumentNullException(nameof(tileTarget));
        _followGridOffset = gridOffset;
        _followPixel = null;
        _followHard = false;

        _followSpeedTilesPerSec = speedTilesPerSec;
        _followSnapTiles = MathF.Max(0f, snapTiles);
        _followSpeedPxPerSec = 0f;
        _followSnapPx = 0f;

        _followOffsetPx = (_mover.PositionSpace == CoordinateSpace.Pixel) ? pixelOffset : Vector2.Zero;

        // clear tween state
        _followEasing = null;
        _followDurationSec = 0f;
    }

    /// <summary>
    /// Start following a <b>grid (tile) target</b> using a duration-based tween (easing).
    /// When easing is active, speed fields are ignored. Snap tolerance is stored for both
    /// pixel and tile spaces; the runtime branch uses the follower's space.
    /// </summary>
    /// <param name="tileTarget">The grid-anchored target to follow.</param>
    /// <param name="durationSec">Tween duration in seconds. Must be &gt; 0.</param>
    /// <param name="easing">Optional easing function in the range [0,1]→[0,1]. If null, linear easing is used.</param>
    /// <param name="snap">Arrival tolerance; applied in the follower's space at runtime.</param>
    /// <param name="gridOffset">Optional tile offset added to the target's grid coordinate.</param>
    /// <param name="pixelOffset">Optional pixel offset applied only when the follower is pixel-space.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tileTarget"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="durationSec"/> ≤ 0.</exception>
    public void FollowTileSoft(IMovableOnSceneLayer tileTarget,
                               float durationSec,
                               Func<float, float>? easing = null,
                               float snap = 0.5f,
                               Vector2 gridOffset = default,
                               Vector2 pixelOffset = default)
    {
        if (durationSec <= 0f)
            throw new ArgumentOutOfRangeException(nameof(durationSec));

        _followGridTarget = tileTarget ?? throw new ArgumentNullException(nameof(tileTarget));
        _followGridOffset = gridOffset;
        _followPixel = null;
        _followHard = false;

        _followOffsetPx = (_mover.PositionSpace == CoordinateSpace.Pixel) ? pixelOffset : Vector2.Zero;

        _followEasing = easing ?? EasingFunctions.Linear;
        _followDurationSec = durationSec;

        // snap in follower’s space at runtime:
        // - pixel follower uses _followSnapPx
        // - grid follower uses _followSnapTiles
        // so just store both clamped and let the branch pick the right one:
        _followSnapPx = MathF.Max(0f, snap);
        _followSnapTiles = MathF.Max(0f, snap);

        // neutralize speed fields
        _followSpeedPxPerSec = 0f;
        _followSpeedTilesPerSec = 0f;
    }

    /// <summary>
    /// Convenience overload of <see cref="FollowTileSoft(IMovableOnSceneLayer, float, Func{float, float}?, float, Vector2, Vector2)"/>
    /// that specifies the easing curve via <see cref="EasingKind"/>.
    /// </summary>
    /// <param name="tileTarget">The grid-anchored target to follow.</param>
    /// <param name="durationSec">Tween duration in seconds. Must be &gt; 0.</param>
    /// <param name="easingKind">Named easing preset.</param>
    /// <param name="snap">Arrival tolerance; applied in the follower's space at runtime.</param>
    /// <param name="gridOffset">Optional tile offset added to the target's grid coordinate.</param>
    /// <param name="pixelOffset">Optional pixel offset applied only when the follower is pixel-space.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tileTarget"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="durationSec"/> ≤ 0.</exception>
    public void FollowTileSoft(IMovableOnSceneLayer tileTarget,
                               float durationSec,
                               EasingKind easingKind,
                               float snap = 0.5f,
                               Vector2 gridOffset = default,
                               Vector2 pixelOffset = default)
    {
        var easing = EasingFunctions.From(easingKind);
        FollowTileSoft(tileTarget, durationSec, easing, snap, gridOffset, pixelOffset);
    }

    /// <summary>
    /// Hard-follow a <b>grid (tile) target</b>. The follower snaps directly to the target
    /// grid coordinate each update (no tweening, no speed). If the follower is pixel-space,
    /// <paramref name="pixelOffset"/> is applied after grid→pixel conversion.
    /// </summary>
    /// <param name="tileTarget">The grid-anchored target to follow.</param>
    /// <param name="gridOffset">Optional tile offset applied to the target's grid coordinate.</param>
    /// <param name="pixelOffset">Optional pixel offset applied only when the follower is pixel-space.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tileTarget"/> is null.</exception>
    public void FollowTileHard(IMovableOnSceneLayer tileTarget,
                               Vector2 gridOffset = default,
                               Vector2 pixelOffset = default)
    {
        _followGridTarget = tileTarget ?? throw new ArgumentNullException(nameof(tileTarget));
        _followGridOffset = gridOffset;
        _followPixel = null;
        _followHard = true;

        _followOffsetPx = (_mover.PositionSpace == CoordinateSpace.Pixel) ? pixelOffset : Vector2.Zero;

        // neutralize speed/tween fields
        _followSpeedTilesPerSec = 0f;
        _followSnapTiles = 0f;
        _followSpeedPxPerSec = 0f;
        _followSnapPx = 0f;
        _followEasing = null;
        _followDurationSec = 0f;
    }

    /// <summary>
    /// Stop following any target and clear all follow/tween state (targets, speeds, offsets, easing, hard mode).
    /// Any in-flight scripted/tweened movement is cancelled immediately.
    /// </summary>
    public void Unfollow()
    {
        _followPixel = null;
        _followGridTarget = null;
        _followEasing = null;
        _followDurationSec = 0f;
        _followHard = false;

        _followSpeedPxPerSec = 0f;
        _followSnapPx = 0f;
        _followSpeedTilesPerSec = 0f;
        _followSnapTiles = 0f;
        _followOffsetPx = Vector2.Zero;
        _followGridOffset = Vector2.Zero;

        CancelScript();
    }

    private bool AdvanceFollow()
    {
        // --- Pixel-follow ---
        if (_followPixel is not null)
        {
            var goalPx = _followPixel() + _followOffsetPx;

            if (_mover.PositionSpace == CoordinateSpace.Grid)
            {
                // grid-space mover following pixel goal...
                return Advance_TargetPixel_MoverGrid(goalPx);
            }
            else
            {
                // pixel mover following pixel goal...
                return Advance_TargetPixel_MoverPixel(goalPx);
            }
        }

        // --- Grid-follow (target provides GRID coords each frame) ---
        if (_followGridTarget is not null)
        {
            var layer = _followGridTarget.SceneLayer;

            // target grid coordinate (+ any grid offset)
            var goalCoordinates = _followGridTarget.GetPosition() + _followGridOffset;

            if (_mover.PositionSpace == CoordinateSpace.Grid)
            {
                // GRID follower -> set/use GRID directly (no conversion)
                return Advance_TargetGrid_MoverGrid(goalCoordinates);
            }
            else
            {
                // PIXEL follower -> convert GRID -> PIXEL, then apply optional pixel offset
                return Advance_TargetGrid_MoverPixel(goalCoordinates, layer);
            }
        }

        return false; // not following this frame
    }

    private bool Advance_TargetPixel_MoverGrid(Vector2 goalPx)
    {
        // follower is grid-space: convert pixel -> grid
        if (_sceneLayer is null || _coords is null)
            throw new InvalidOperationException("Pixel->Grid follow requires scene layer/coords.");

        var gridPt = _coords.GetSceneLayerCoordinatesAtPixel(_sceneLayer, new PointF(goalPx.X, goalPx.Y));
        var goalGrid = new Vector2(gridPt.X, gridPt.Y);

        if (_followHard)
        {
            _mover.SetPosition(goalGrid);
            return true;
        }

        if (_followEasing is not null && _followDurationSec > 0f)
        {
            MoveTo(goalGrid, _followDurationSec, _followEasing, _followSnapTiles);
            return false;
        }

        MoveToward(goalGrid, _followSpeedTilesPerSec, _followSnapTiles);
        return false;
    }

    private bool Advance_TargetPixel_MoverPixel(Vector2 goalPx)
    {
        if (_followHard)
        {
            _mover.SetPosition(goalPx);
            return true;
        }

        // tweened follow
        if (_followEasing is not null && _followDurationSec > 0f)
        {
            MoveTo(goalPx, _followDurationSec, _followEasing, _followSnapPx);
        }
        else
        {
            // constant-speed pursue
            MoveToward(goalPx, _followSpeedPxPerSec, _followSnapPx);
        }

        return false; // we scheduled a script; let scripted stage consume this frame
    }

    private bool Advance_TargetGrid_MoverGrid(Vector2 goalCoordinates)
    {
        if (_followHard)
        {
            _mover.SetPosition(goalCoordinates);
            return true;
        }

        if (_followEasing is not null && _followDurationSec > 0f)
        {
            MoveTo(goalCoordinates, _followDurationSec, _followEasing, _followSnapTiles);
            return false;
        }

        // soft follow in tiles/sec and snap in tiles
        MoveToward(goalCoordinates, _followSpeedTilesPerSec, _followSnapTiles);
        return false;
    }

    private bool Advance_TargetGrid_MoverPixel(Vector2 goalCoordinates, SceneLayer sceneLayer)
    {
        var targetPx = sceneLayer.CoordinateSystem.GetAnchorPixelAtSceneLayerCoordinates(sceneLayer, new PointF(goalCoordinates.X, goalCoordinates.Y));
        var goalPx = new Vector2(targetPx.X, targetPx.Y) + _followOffsetPx;

        if (_followHard)
        {
            _mover.SetPosition(goalPx);
            return true;
        }

        if (_followEasing is not null && _followDurationSec > 0f)
        {
            MoveTo(goalPx, _followDurationSec, _followEasing, _followSnapPx);
            return false;
        }

        // tiles/sec → px/sec (sample +X to estimate px-per-tile length)
        var pxRight = sceneLayer.CoordinateSystem.GetAnchorPixelAtSceneLayerCoordinates(sceneLayer, new PointF(goalCoordinates.X + 1f, goalCoordinates.Y));
        float pxPerTile = MathF.Max(1f, new Vector2(pxRight.X - targetPx.X, pxRight.Y - targetPx.Y).Length());
        float speedPxPerSec = _followSpeedTilesPerSec * pxPerTile;
        float snapPx = _followSnapTiles * pxPerTile;

        MoveToward(goalPx, speedPxPerSec, snapPx);
        return false;
    }
}