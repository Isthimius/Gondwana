using Gondwana.Physics.Movement.Easing;

namespace Gondwana.Effects;

/// <summary>
/// Base class for transient effects applied to a View or SceneLayer.
/// </summary>
/// <remarks>
/// Display effects update presentation-only state. They do not move world objects,
/// change collision geometry, or alter a SceneLayer's canonical origin.
/// </remarks>
public abstract class DisplayEffect
{
    private EffectsManager? _owner;
    private object? _target;
    private float _elapsedSeconds;

    private protected DisplayEffect(
        float durationSeconds,
        EasingKind easing = EasingKind.Linear)
    {
        DurationSeconds = Math.Max(0f, durationSeconds);
        Easing = easing;
    }

    /// <summary>Occurs when the effect reaches its requested duration.</summary>
    public event Action<DisplayEffect>? Completed;

    /// <summary>Occurs when the effect is cancelled or replaced.</summary>
    public event Action<DisplayEffect>? Cancelled;

    /// <summary>Gets the effect's unique identifier.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Gets the requested effect duration in seconds.</summary>
    public float DurationSeconds { get; }

    /// <summary>Gets the easing curve used to transform normalized progress.</summary>
    public EasingKind Easing { get; }

    /// <summary>Gets the current lifecycle state.</summary>
    public EffectStatus Status { get; private set; } = EffectStatus.Pending;

    /// <summary>Gets the normalized, uneased progress in the range 0 through 1.</summary>
    public float Progress { get; private set; }

    internal object Target => _target
        ?? throw new InvalidOperationException("The effect has not been started.");

    internal abstract EffectChannel Channel { get; }

    internal abstract bool SupportsTarget(object target);

    private protected TTarget GetTarget<TTarget>() where TTarget : class =>
        Target as TTarget
        ?? throw new InvalidOperationException(
            $"{GetType().Name} cannot operate on target type {Target.GetType().Name}.");

    internal void StartInternal(EffectsManager owner, object target)
    {
        if (Status != EffectStatus.Pending)
            throw new InvalidOperationException("An effect instance can only be started once.");

        _owner = owner;
        _target = target;
        Status = EffectStatus.Running;

        OnStarting();

        if (DurationSeconds <= 0f)
        {
            Progress = 1f;
            ApplyProgress(1f);
            CompleteInternal();
            return;
        }

        ApplyProgress(EasingFunctions.From(Easing)(0f));
    }

    internal bool AdvanceInternal(float deltaSeconds)
    {
        if (Status != EffectStatus.Running)
            return true;

        _elapsedSeconds = Math.Min(
            DurationSeconds,
            _elapsedSeconds + Math.Max(0f, deltaSeconds));

        Progress = DurationSeconds <= 0f
            ? 1f
            : Math.Clamp(_elapsedSeconds / DurationSeconds, 0f, 1f);

        float easedProgress = EasingFunctions.From(Easing)(Progress);
        ApplyProgress(easedProgress);

        if (Progress >= 1f)
            CompleteInternal();

        return Status != EffectStatus.Running;
    }

    /// <summary>
    /// Cancels the effect and restores the presentation state that existed when it began.
    /// </summary>
    public void Cancel() => _owner?.Cancel(this);

    internal void CancelInternal(bool restoreState)
    {
        if (Status != EffectStatus.Running)
            return;

        if (restoreState)
            RestoreOriginalState();

        Status = EffectStatus.Cancelled;
        _owner = null;
        Cancelled?.Invoke(this);
    }

    private void CompleteInternal()
    {
        if (Status != EffectStatus.Running)
            return;

        OnCompleted();
        Status = EffectStatus.Completed;
        _owner = null;
        Completed?.Invoke(this);
    }

    private protected virtual void OnStarting()
    {
    }

    private protected abstract void ApplyProgress(float progress);

    private protected virtual void OnCompleted()
    {
    }

    private protected abstract void RestoreOriginalState();
}

internal enum EffectChannel
{
    Transform,
    Opacity,
    Reveal,
    Zoom
}
