using Gondwana.Physics.Movement.Easing;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Effects;

/// <summary>Base implementation shared by fade-in and fade-out effects.</summary>
public abstract class FadeEffect : DisplayEffect
{
    private readonly float _targetOpacity;
    private readonly bool _startTransparentWhenOpaque;
    private float _originalOpacity;
    private float _startOpacity;

    private protected FadeEffect(
        float targetOpacity,
        float durationSeconds,
        EasingKind easing,
        bool startTransparentWhenOpaque)
        : base(durationSeconds, easing)
    {
        _targetOpacity = Math.Clamp(targetOpacity, 0f, 1f);
        _startTransparentWhenOpaque = startTransparentWhenOpaque;
    }

    internal override EffectChannel Channel => EffectChannel.Opacity;

    internal override bool SupportsTarget(object target) =>
        target is View or SceneLayer;

    private protected override void OnStarting()
    {
        _originalOpacity = EffectTargetAccess.GetOpacity(Target);
        _startOpacity = _startTransparentWhenOpaque && _originalOpacity >= 0.9999f
            ? 0f
            : _originalOpacity;

        EffectTargetAccess.SetOpacity(Target, _startOpacity);
    }

    private protected override void ApplyProgress(float progress) =>
        EffectTargetAccess.SetOpacity(
            Target,
            _startOpacity + (_targetOpacity - _startOpacity) * progress);

    private protected override void RestoreOriginalState() =>
        EffectTargetAccess.SetOpacity(Target, _originalOpacity);
}

/// <summary>Fades a View or SceneLayer to fully opaque.</summary>
public sealed class FadeInEffect : FadeEffect
{
    /// <summary>Creates a fade-in effect.</summary>
    public FadeInEffect(
        float durationSeconds,
        EasingKind easing = EasingKind.Linear)
        : base(1f, durationSeconds, easing, startTransparentWhenOpaque: true)
    {
    }
}

/// <summary>Fades a View or SceneLayer to fully transparent.</summary>
public sealed class FadeOutEffect : FadeEffect
{
    /// <summary>Creates a fade-out effect.</summary>
    public FadeOutEffect(
        float durationSeconds,
        EasingKind easing = EasingKind.Linear)
        : base(0f, durationSeconds, easing, startTransparentWhenOpaque: false)
    {
    }
}
