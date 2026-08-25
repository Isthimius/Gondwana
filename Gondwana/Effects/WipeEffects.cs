using Gondwana.Physics.Movement.Easing;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Effects;

/// <summary>Base implementation shared by directional fill and erase effects.</summary>
public abstract class WipeEffect : DisplayEffect
{
    private readonly bool _isFill;
    private float _originalReveal;
    private EffectDirection _originalDirection;
    private float _startReveal;
    private float _targetReveal;

    private protected WipeEffect(
        EffectDirection direction,
        float durationSeconds,
        EasingKind easing,
        bool isFill)
        : base(durationSeconds, easing)
    {
        if (direction == EffectDirection.None)
            throw new ArgumentOutOfRangeException(nameof(direction));

        Direction = direction;
        _isFill = isFill;
    }

    /// <summary>Gets the direction in which the wipe travels.</summary>
    public EffectDirection Direction { get; }

    internal override EffectChannel Channel => EffectChannel.Reveal;

    internal override bool SupportsTarget(object target) =>
        target is View or SceneLayer;

    private protected override void OnStarting()
    {
        _originalReveal = EffectTargetAccess.GetReveal(Target);
        _originalDirection = EffectTargetAccess.GetRevealDirection(Target);

        if (_isFill)
        {
            _startReveal = _originalReveal >= 0.9999f ? 0f : _originalReveal;
            _targetReveal = 1f;
        }
        else
        {
            _startReveal = _originalReveal;
            _targetReveal = 0f;
        }

        EffectTargetAccess.SetReveal(Target, _startReveal, Direction);
    }

    private protected override void ApplyProgress(float progress) =>
        EffectTargetAccess.SetReveal(
            Target,
            _startReveal + (_targetReveal - _startReveal) * progress,
            Direction);

    private protected override void RestoreOriginalState() =>
        EffectTargetAccess.SetReveal(Target, _originalReveal, _originalDirection);
}

/// <summary>Directionally reveals a View or SceneLayer.</summary>
public sealed class FillEffect : WipeEffect
{
    /// <summary>Creates a directional fill effect.</summary>
    public FillEffect(
        EffectDirection direction,
        float durationSeconds,
        EasingKind easing = EasingKind.Linear)
        : base(direction, durationSeconds, easing, isFill: true)
    {
    }
}

/// <summary>Directionally removes a View or SceneLayer from presentation.</summary>
public sealed class EraseEffect : WipeEffect
{
    /// <summary>Creates a directional erase effect.</summary>
    public EraseEffect(
        EffectDirection direction,
        float durationSeconds,
        EasingKind easing = EasingKind.Linear)
        : base(direction, durationSeconds, easing, isFill: false)
    {
    }
}
