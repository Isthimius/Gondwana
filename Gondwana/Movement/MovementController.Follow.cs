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
    /// Follow a PIXEL target at constant speed. 
    /// Speed/snap units follow the follower's space:
    ///  - Pixel follower: pixels/sec and pixels
    ///  - Grid follower:  tiles/sec and tiles
    /// </summary>
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

        _followOffsetPx = offsetPx;
        _followEasing = easing ?? EasingFunctions.Linear;
        _followDurationSec = durationSec;

        // neutralize speed fields
        _followSpeedPxPerSec = 0f;
        _followSnapPx = MathF.Max(0f, snapPx);
        _followSpeedTilesPerSec = 0f;
        _followSnapTiles = 0f;
    }

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
    /// Hard follow a PIXEL target (instant snap each frame).
    /// Pixel offset applies only if the follower is pixel-space; ignored for grid followers.
    /// </summary>
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
    /// Follow a GRID (tile) target at constant speed.
    /// Speed/snap are always tiles/sec and tiles. Pixel offset is applied only if the follower is pixel-space.
    /// </summary>
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
    /// Hard follow a GRID (tile) target (instant snap each frame).
    /// Pixel offset is applied only if the follower is pixel-space.
    /// </summary>
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

    public void Unfollow()
    {
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

        var gridPt = _coords.GetSceneLayerCoordinatesAtPixel(_sceneLayer, new Point((int)goalPx.X, (int)goalPx.Y));
        var goalGrid = new Vector2(gridPt.X, gridPt.Y);

        if (_followHard) { _mover.SetPosition(goalGrid); return true; }
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