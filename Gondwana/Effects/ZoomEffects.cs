using Gondwana.Physics.Movement.Easing;
using Gondwana.Rendering.Views;

namespace Gondwana.Effects;

/// <summary>Base implementation shared by zoom-in and zoom-out effects.</summary>
public abstract class ZoomEffect : DisplayEffect
{
    private float _originalZoom;
    private float _clampedTargetZoom;

    private protected ZoomEffect(float targetZoom, float durationSeconds)
        : base(durationSeconds, EasingKind.Linear)
    {
        if (targetZoom <= 0f)
            throw new ArgumentOutOfRangeException(nameof(targetZoom));

        TargetZoom = targetZoom;
    }

    /// <summary>Gets the requested final zoom factor.</summary>
    public float TargetZoom { get; }

    internal override EffectChannel Channel => EffectChannel.Zoom;

    internal override bool SupportsTarget(object target) => target is View;

    private protected override void OnStarting()
    {
        var view = GetTarget<View>();
        _originalZoom = view.Viewport.Zoom;
        _clampedTargetZoom = Math.Clamp(TargetZoom, view.MinZoom, view.MaxZoom);
        view.Viewport.ZoomToOverDuration(_clampedTargetZoom, DurationSeconds);
    }

    // View.Update() advances the existing Viewport zoom animator. The effect
    // manager owns only lifecycle, replacement, and completion notification.
    private protected override void ApplyProgress(float progress)
    {
    }

    private protected override void OnCompleted() =>
        GetTarget<View>().Viewport.SnapZoom(_clampedTargetZoom);

    private protected override void RestoreOriginalState() =>
        GetTarget<View>().Viewport.SnapZoom(_originalZoom);
}

/// <summary>Animates a View to a larger or otherwise explicitly supplied zoom factor.</summary>
public sealed class ZoomInEffect : ZoomEffect
{
    /// <summary>Creates a zoom-in effect.</summary>
    public ZoomInEffect(float targetZoom, float durationSeconds)
        : base(targetZoom, durationSeconds)
    {
    }
}

/// <summary>Animates a View to a smaller or otherwise explicitly supplied zoom factor.</summary>
public sealed class ZoomOutEffect : ZoomEffect
{
    /// <summary>Creates a zoom-out effect.</summary>
    public ZoomOutEffect(float targetZoom, float durationSeconds)
        : base(targetZoom, durationSeconds)
    {
    }
}
