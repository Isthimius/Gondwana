using System.ComponentModel;
using Gondwana.Drawing.Animation;
using Gondwana.Tooling.Animations.Editing;

namespace Gondwana.Tooling.Animations.WinForms;

internal sealed class AnimationPropertyAdapter
{
    private readonly AnimationDocument _document;

    public AnimationPropertyAdapter(AnimationDocument document)
    {
        _document = document;
    }

    [Category("Animation")]
    [DisplayName("Key")]
    [Description("Global cycle key used by Animator.StartAnimation and SetCurrentCycle.")]
    public string Key
    {
        get => _document.Definition.Key;
        set => Set(
            _document.Definition.Key,
            value ?? string.Empty,
            v => _document.Definition.Key = v);
    }

    [Category("Playback")]
    [DisplayName("Throttle time")]
    [Description("Seconds between frame transitions.")]
    public double ThrottleTime
    {
        get => _document.Definition.ThrottleTime;
        set => Set(
            _document.Definition.ThrottleTime,
            value,
            v => _document.Definition.ThrottleTime = v);
    }

    [Category("Playback")]
    [DisplayName("Cycle type")]
    public CycleType CycleType
    {
        get => _document.Definition.CycleType;
        set => Set(
            _document.Definition.CycleType,
            value,
            v => _document.Definition.CycleType = v);
    }

    [Category("Playback")]
    [DisplayName("Hide tile on cycle end")]
    public bool HideTileOnCycleEnd
    {
        get => _document.Definition.HideTileOnCycleEnd;
        set => Set(
            _document.Definition.HideTileOnCycleEnd,
            value,
            v => _document.Definition.HideTileOnCycleEnd = v);
    }

    [Category("Playback")]
    [DisplayName("Next cycle key")]
    [Description("Optional cycle key to transition to when this cycle completes. Empty means self.")]
    public string NextCycleKey
    {
        get => _document.Definition.NextCycleKey ?? string.Empty;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();

            if (string.Equals(
                    _document.Definition.NextCycleKey,
                    normalized,
                    StringComparison.Ordinal))
            {
                return;
            }

            _document.Definition.NextCycleKey = normalized;
            _document.MarkChanged();
        }
    }

    [Category("Source")]
    [DisplayName("Source kind")]
    [ReadOnly(true)]
    public string SourceKind =>
        _document.Definition.Source.Kind.ToString();

    private void Set<T>(
        T current,
        T value,
        Action<T> assign)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
            return;

        assign(value);
        _document.MarkChanged();
    }
}
