using System.Numerics;
using Gondwana.Movement.Easing;

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

    public void FollowSoft(Func<Vector2> getPixelPos, float speedPxPerSec,
                           float snapPx = 0.5f, Vector2 offsetPx = default)
    {
        if (speedPxPerSec <= 0)
            throw new ArgumentOutOfRangeException(nameof(speedPxPerSec));

        _followPixel = getPixelPos ?? throw new ArgumentNullException(nameof(getPixelPos));
        _followOffsetPx = offsetPx;
        _followHard = false;
        _followSpeedPxPerSec = speedPxPerSec;
        _followSnapPx = MathF.Max(0f, snapPx);
        _followGridTarget = null;
    }

    public void FollowSoft(Func<Vector2> getPixelPos, float durationSec,
                       Func<float, float>? easing = null,
                       float snapPx = 0.5f,
                       Vector2 offsetPx = default)
    {
        if (durationSec <= 0f)
            throw new ArgumentOutOfRangeException(nameof(durationSec));

        _followPixel = getPixelPos ?? throw new ArgumentNullException(nameof(getPixelPos));
        _followOffsetPx = offsetPx;
        _followHard = false;
        _followSpeedPxPerSec = 0f; // not used in tween mode
        _followSnapPx = snapPx;
        _followEasing = easing ?? EasingFunctions.Linear;
        _followDurationSec = durationSec;
    }

    public void FollowSoft(Func<Vector2> getPixelPos, float durationSec,
                   EasingKind easingKind,
                   float snapPx = 0.5f,
                   Vector2 offsetPx = default)
    {
        var easing = EasingFunctions.From(easingKind);
        FollowSoft(getPixelPos, durationSec, easing, snapPx, offsetPx);
    }

    public void FollowSoft(IMovableOnSceneLayer gridTarget, float speedTilesPerSec,
                           float snapTiles = 0.25f, Vector2 gridOffset = default)
    {
        if (speedTilesPerSec <= 0) throw new ArgumentOutOfRangeException(nameof(speedTilesPerSec));
        _followGridTarget = gridTarget ?? throw new ArgumentNullException(nameof(gridTarget));
        _followGridOffset = gridOffset;
        _followHard = false;
        _followSpeedTilesPerSec = speedTilesPerSec;
        _followSnapTiles = MathF.Max(0f, snapTiles);
        _followPixel = null;
    }

    public void FollowHard(Func<Vector2> getPixelPos, Vector2 offsetPx = default)
    {
        _followPixel = getPixelPos ?? throw new ArgumentNullException(nameof(getPixelPos));
        _followOffsetPx = offsetPx;
        _followHard = true;
        _followGridTarget = null; // ensure only one mode is active
    }

    public void FollowHard(IMovableOnSceneLayer gridTarget, Vector2 gridOffset = default)
    {
        _followGridTarget = gridTarget ?? throw new ArgumentNullException(nameof(gridTarget));
        _followGridOffset = gridOffset;
        _followHard = true;
        _followPixel = null;
    }

    public void Unfollow()
    {
        _followPixel = null;
        _followGridTarget = null;
        _followEasing = null;
        _followDurationSec = 0f;
        CancelScript();
    }

    private bool AdvanceFollow()
    {
        // --- Pixel-follow ---
        if (_followPixel is not null)
        {
            var goal = _followPixel() + _followOffsetPx;

            if (_followHard)
            {
                _mover.SetPosition(goal);
                return true;
            }

            // tweened follow
            if (_followEasing is not null && _followDurationSec > 0f)
            {
                MoveTo(goal, _followDurationSec, _followEasing, _followSnapPx);
            }
            else
            {
                // constant-speed pursue
                MoveToward(goal, _followSpeedPxPerSec, _followSnapPx);
            }

            return false; // we scheduled a script; let scripted stage consume this frame
        }

        // --- Grid-follow (convert grid→pixel each frame) ---
        if (_followGridTarget is not null)
        {
            var layer = _followGridTarget.SceneLayer;
            var coords = layer.CoordinateSystem
                        ?? throw new InvalidOperationException("Follow target layer has no CoordinateSystem.");

            var grid = _followGridTarget.GetPosition() + _followGridOffset;
            var pxNow = coords.GetAnchorPixelAtSceneLayerCoordinates(layer, new System.Drawing.PointF(grid.X, grid.Y));
            var goalPx = new Vector2(pxNow.X, pxNow.Y);

            if (_followHard)
            {
                _mover.SetPosition(goalPx);
                return true;
            }
            else
            {
                // tiles/sec → px/sec
                var pxRight = coords.GetAnchorPixelAtSceneLayerCoordinates(layer, new System.Drawing.PointF(grid.X + 1f, grid.Y));
                float pxPerTile = MathF.Max(1f, new Vector2(pxRight.X - pxNow.X, pxRight.Y - pxNow.Y).Length());

                float speedPxPerSec = _followSpeedTilesPerSec * pxPerTile;
                float snapPx = _followSnapTiles * pxPerTile;

                MoveToward(goalPx, speedPxPerSec, snapPx);
            }

            return false; // scheduled a script; let scripted stage handle it
        }

        return false; // not following this frame
    }
}
