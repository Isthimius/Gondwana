using System.Numerics;

namespace Gondwana.Movement;

public sealed partial class MovementController
{
    // --- Pixel-follow (DirectDrawing → DirectDrawing)
    private Func<Vector2>? _followPixel;   // returns current target pixel position each frame
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

    public void FollowHard(Func<Vector2> getPixelPos, Vector2 offsetPx = default)
    {
        _followPixel = getPixelPos ?? throw new ArgumentNullException(nameof(getPixelPos));
        _followOffsetPx = offsetPx;
        _followHard = true;
        _followGridTarget = null; // ensure only one mode is active
    }

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
        _followEasing = easing ?? EasingFunctions.Linear; // assume your tween system has these
        _followDurationSec = durationSec;
    }

    public void FollowHard(IMovableOnSceneLayer gridTarget, Vector2 gridOffset = default)
    {
        _followGridTarget = gridTarget ?? throw new ArgumentNullException(nameof(gridTarget));
        _followGridOffset = gridOffset;
        _followHard = true;
        _followPixel = null;
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

    public void Unfollow()
    {
        _followPixel = null;
        _followGridTarget = null;
        _followEasing = null;
        _followDurationSec = 0f;
        CancelScript(ref _state);
    }
}
