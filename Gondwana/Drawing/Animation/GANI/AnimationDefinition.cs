namespace Gondwana.Drawing.Animation.GANI;

/// <summary>
/// Represents the root definition of an animation in the GANI (Gondwana Animation) file format.
/// </summary>
public sealed class AnimationDefinition
{
    private AnimationDefinitionSource _source = AnimationDefinitionSource.None();

    /// <summary>
    /// Gets or sets the globally unique animation key used by <see cref="Cycle"/>.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time in seconds between frame transitions.
    /// </summary>
    public double ThrottleTime { get; set; }

    /// <summary>
    /// Gets or sets the sequence playback pattern.
    /// </summary>
    public CycleType CycleType { get; set; } = CycleType.Simple;

    /// <summary>
    /// Gets or sets whether the owning tile should be hidden when this cycle ends.
    /// </summary>
    public bool HideTileOnCycleEnd { get; set; }

    /// <summary>
    /// Gets or sets the key of the cycle to transition to when this cycle ends.
    /// A null or empty value uses the runtime default of transitioning to this cycle.
    /// </summary>
    public string? NextCycleKey { get; set; }

    /// <summary>
    /// Gets or sets authoring-time locations for logical GTS dependencies used by this animation.
    /// Runtime materialization still resolves frames through the registered tilesheet name.
    /// </summary>
    public List<AnimationTilesheetSourceDefinition> TilesheetSources { get; set; } = [];

    /// <summary>
    /// Gets or sets the ordered frame references in this animation.
    /// </summary>
    public List<AnimationFrameDefinition> Frames { get; set; } = [];

    /// <summary>
    /// Gets or sets provenance metadata describing where this definition came from.
    /// </summary>
    public AnimationDefinitionSource Source
    {
        get => _source;
        set => _source = value ?? throw new ArgumentNullException(nameof(value));
    }
}
