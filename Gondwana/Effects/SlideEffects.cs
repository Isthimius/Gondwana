using System.Drawing;
using Gondwana.Physics.Movement.Easing;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Effects;

/// <summary>Base implementation shared by slide-in and slide-out effects.</summary>
public abstract class SlideEffect : DisplayEffect
{
    private readonly bool _isSlideIn;
    private PointF _originalFactor;
    private PointF _originalPixels;
    private PointF _startFactor;
    private PointF _targetFactor;

    private protected SlideEffect(
        EffectDirection direction,
        float durationSeconds,
        EasingKind easing,
        bool isSlideIn)
        : base(durationSeconds, easing)
    {
        if (direction == EffectDirection.None)
            throw new ArgumentOutOfRangeException(nameof(direction));

        Direction = direction;
        _isSlideIn = isSlideIn;
    }

    /// <summary>Gets the direction in which the target travels.</summary>
    public EffectDirection Direction { get; }

    internal override EffectChannel Channel => EffectChannel.Transform;

    internal override bool SupportsTarget(object target) =>
        target is View or SceneLayer;

    private protected override void OnStarting()
    {
        _originalFactor = EffectTargetAccess.GetOffsetFactor(Target);
        _originalPixels = EffectTargetAccess.GetOffsetPixels(Target);

        PointF travel = GetTravelFactor(Direction);

        if (_isSlideIn)
        {
            _startFactor = IsNearlyZero(_originalFactor)
                ? new PointF(-travel.X, -travel.Y)
                : _originalFactor;

            _targetFactor = PointF.Empty;
        }
        else
        {
            _startFactor = _originalFactor;
            _targetFactor = travel;
        }

        EffectTargetAccess.SetTransform(
            Target,
            _startFactor,
            _originalPixels);
    }

    private protected override void ApplyProgress(float progress)
    {
        var factor = new PointF(
            _startFactor.X + (_targetFactor.X - _startFactor.X) * progress,
            _startFactor.Y + (_targetFactor.Y - _startFactor.Y) * progress);

        var pixels = new PointF(
            _originalPixels.X * (1f - progress),
            _originalPixels.Y * (1f - progress));

        EffectTargetAccess.SetTransform(Target, factor, pixels);
    }

    private protected override void RestoreOriginalState() =>
        EffectTargetAccess.SetTransform(Target, _originalFactor, _originalPixels);

    private static bool IsNearlyZero(PointF value) =>
        Math.Abs(value.X) < 0.0001f && Math.Abs(value.Y) < 0.0001f;

    private static PointF GetTravelFactor(EffectDirection direction) => direction switch
    {
        EffectDirection.FromLeftToRight => new PointF(1f, 0f),
        EffectDirection.FromRightToLeft => new PointF(-1f, 0f),
        EffectDirection.FromTopToBottom => new PointF(0f, 1f),
        EffectDirection.FromBottomToTop => new PointF(0f, -1f),
        EffectDirection.FromTopLeftToBottomRight => new PointF(1f, 1f),
        EffectDirection.FromTopRightToBottomLeft => new PointF(-1f, 1f),
        EffectDirection.FromBottomLeftToTopRight => new PointF(1f, -1f),
        EffectDirection.FromBottomRightToTopLeft => new PointF(-1f, -1f),
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };
}

/// <summary>Slides a View or SceneLayer into its normal presentation position.</summary>
public sealed class SlideInEffect : SlideEffect
{
    /// <summary>Creates a slide-in effect.</summary>
    public SlideInEffect(
        EffectDirection direction,
        float durationSeconds,
        EasingKind easing = EasingKind.EaseOutCubic)
        : base(direction, durationSeconds, easing, isSlideIn: true)
    {
    }
}

/// <summary>Slides a View or SceneLayer out of its normal presentation position.</summary>
public sealed class SlideOutEffect : SlideEffect
{
    /// <summary>Creates a slide-out effect.</summary>
    public SlideOutEffect(
        EffectDirection direction,
        float durationSeconds,
        EasingKind easing = EasingKind.EaseInCubic)
        : base(direction, durationSeconds, easing, isSlideIn: false)
    {
    }
}
