namespace Gondwana.State;

/// <summary>
/// Represents a single engine state file configuration.
/// </summary>
public class EngineStateFile
{
    /// <summary>
    /// Unique identifier for the state file entry.
    /// </summary>
    public string ID { get; set; } = string.Empty;

    /// <summary>
    /// Path to a serialized <see cref="EngineState"/> instance.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Whether the file is in binary format.
    /// </summary>
    public bool IsBinary { get; set; } = false;

    /// <summary>
    /// Whether to load this state file at engine startup.
    /// </summary>
    public bool LoadAtStartup { get; set; } = true;
}
