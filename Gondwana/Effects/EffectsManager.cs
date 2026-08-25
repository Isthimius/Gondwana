using System.Collections.ObjectModel;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Timers;

namespace Gondwana.Effects;

/// <summary>
/// Owns and advances display effects for one RenderSurfaceHost.
/// </summary>
public sealed class EffectsManager : IDisposable
{
    private readonly object _sync = new();
    private readonly List<DisplayEffect> _activeEffects = [];
    private readonly RenderSurfaceHostBase _host;
    private long _lastTick = HighResTimer.GetCurrentTick();
    private bool _disposed;

    internal EffectsManager(RenderSurfaceHostBase host) =>
        _host = host ?? throw new ArgumentNullException(nameof(host));

    /// <summary>Gets a snapshot of the effects that are currently running.</summary>
    public ReadOnlyCollection<DisplayEffect> ActiveEffects
    {
        get
        {
            lock (_sync)
                return _activeEffects.ToList().AsReadOnly();
        }
    }

    /// <summary>Starts an effect targeting a View owned by this render surface.</summary>
    public TEffect Run<TEffect>(View target, TEffect effect)
        where TEffect : DisplayEffect => RunCore(target, effect);

    /// <summary>Starts an effect targeting a SceneLayer in the currently bound Scene.</summary>
    public TEffect Run<TEffect>(SceneLayer target, TEffect effect)
        where TEffect : DisplayEffect => RunCore(target, effect);

    /// <summary>Cancels a running effect and restores its original presentation state.</summary>
    public void Cancel(DisplayEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        bool removed;
        lock (_sync)
            removed = _activeEffects.Remove(effect);

        if (!removed)
            return;

        effect.CancelInternal(restoreState: true);
        Invalidate(effect.Target);
    }

    /// <summary>Cancels every running effect owned by this manager.</summary>
    public void CancelAll()
    {
        DisplayEffect[] snapshot;

        lock (_sync)
        {
            snapshot = _activeEffects.ToArray();
            _activeEffects.Clear();
        }

        foreach (var effect in snapshot)
        {
            effect.CancelInternal(restoreState: true);
            Invalidate(effect.Target);
        }
    }

    internal void Update(long tick)
    {
        float deltaSeconds = HighResTimer.GetDuration(_lastTick, tick);
        _lastTick = tick;
        Advance(deltaSeconds);
    }

    internal void Advance(float deltaSeconds)
    {
        DisplayEffect[] snapshot;

        lock (_sync)
            snapshot = _activeEffects.ToArray();

        foreach (var effect in snapshot)
        {
            if (!OwnsTarget(effect.Target))
            {
                RemoveAndCancel(effect, restoreState: false);
                continue;
            }

            bool finished = effect.AdvanceInternal(deltaSeconds);
            Invalidate(effect.Target);

            if (finished)
            {
                lock (_sync)
                    _activeEffects.Remove(effect);
            }
        }
    }

    internal void Invalidate(object target)
    {
        if (target is View or SceneLayer)
            _host.Scene.FullRefreshNeeded = true;
    }

    private TEffect RunCore<TEffect>(object target, TEffect effect)
        where TEffect : DisplayEffect
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(effect);

        if (!OwnsTarget(target))
            throw new ArgumentException(
                "The effect target is not owned by this render surface host.",
                nameof(target));

        if (!effect.SupportsTarget(target))
            throw new ArgumentException(
                $"{effect.GetType().Name} does not support {target.GetType().Name} targets.",
                nameof(effect));

        DisplayEffect? replaced;

        lock (_sync)
        {
            if (effect.Status != EffectStatus.Pending)
                throw new InvalidOperationException("An effect instance can only be run once.");

            replaced = _activeEffects.FirstOrDefault(
                active => ReferenceEquals(active.Target, target)
                          && active.Channel == effect.Channel);

            if (replaced is not null)
                _activeEffects.Remove(replaced);

            _activeEffects.Add(effect);
        }

        // Preserve the current presentation value when replacing an effect so the
        // new effect can continue from it without a one-frame reset.
        replaced?.CancelInternal(restoreState: false);

        try
        {
            effect.StartInternal(this, target);
            Invalidate(target);
        }
        catch
        {
            lock (_sync)
                _activeEffects.Remove(effect);

            throw;
        }

        if (effect.Status != EffectStatus.Running)
        {
            lock (_sync)
                _activeEffects.Remove(effect);
        }

        return effect;
    }

    private bool OwnsTarget(object target) => target switch
    {
        View view => _host.ViewManager.Views.Contains(view),
        SceneLayer layer => ReferenceEquals(layer.Scene, _host.Scene)
                            && _host.Scene.SceneLayers.Contains(layer),
        _ => false
    };

    private void RemoveAndCancel(DisplayEffect effect, bool restoreState)
    {
        lock (_sync)
            _activeEffects.Remove(effect);

        effect.CancelInternal(restoreState);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CancelAll();
    }
}
