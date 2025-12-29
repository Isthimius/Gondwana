namespace Gondwana.Configuration;

public sealed record StateFileMount
{
    /// <summary>
    /// Path to the state file on disk.
    /// </summary>
    public string File { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether the state file is stored in compressed (GZip) form.
    /// </summary>
    public bool IsCompressed { get; init; } = false;

    /// <summary>
    /// Indicates whether values loaded from this state file should overwrite
    /// existing engine state when a conflict occurs.
    /// <para>
    /// When <c>false</c>, existing state values are preserved and only missing
    /// elements are populated. When <c>true</c>, values from this file take
    /// precedence during merge operations.
    /// </para>
    /// </summary>
    public bool OverwriteExisting { get; init; } = false;

    /// <summary>
    /// Specifies which portions of the engine state should be restored from
    /// this state file.
    /// <para>
    /// This allows selective loading of state components (such as assets,
    /// configuration, or runtime data) rather than restoring the entire engine
    /// state unconditionally.
    /// </para>
    /// </summary>
    public EngineStateParts EngineStateParts { get; init; } = EngineStateParts.All;
}
