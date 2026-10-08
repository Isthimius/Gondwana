using System.ComponentModel;
using Gondwana.Drawing.Animation;
using Gondwana.Tooling.Animations.Editing;

namespace Gondwana.Tooling.Animations.WinForms;

internal sealed class AnimationPropertyAdapter
{
    private readonly AnimationDocument _document;

    /// <summary>
    /// Gets or sets the selected frame index.
    /// </summary>
    [Browsable(false)]
    public int SelectedFrameIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the duration seconds.
    /// </summary>
    [Category("Selected frame")]
    [Description("Display duration in seconds. Clear to use Throttle time. Select a frame in the frame list first.")]
    public double? DurationSeconds
    {
        get => SelectedFrameIndex >= 0 && SelectedFrameIndex < _document.Definition.Frames.Count
            ? _document.Definition.Frames[SelectedFrameIndex].DurationSeconds : null;
        set
        {
            if (SelectedFrameIndex < 0 || SelectedFrameIndex >= _document.Definition.Frames.Count) return;
            if (value is { } duration && (!double.IsFinite(duration) || duration <= 0))
                throw new ArgumentOutOfRangeException(nameof(value), "Duration must be finite and positive.");
            _document.Definition.Frames[SelectedFrameIndex].DurationSeconds = value;
            _document.MarkChanged();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <c>AnimationPropertyAdapter</c> class.
    /// </summary>
    /// <param name="document">The document displayed or edited by the control.</param>
    public AnimationPropertyAdapter(AnimationDocument document)
    {
        _document = document;
    }

    /// <summary>
    /// Gets or sets the key.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the throttle time.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the cycle type.
    /// </summary>
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

    /// <summary>
    /// Gets or sets whether hide tile on cycle end is enabled.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the next cycle key.
    /// </summary>
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

    /// <summary>
    /// Gets the source kind.
    /// </summary>
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
