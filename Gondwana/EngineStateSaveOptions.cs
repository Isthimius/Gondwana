namespace Gondwana;

/// <summary>Controls how EngineState persists a definition.</summary>
public enum DefinitionPersistence
{
    /// <summary>Embed the current definition in the state file.</summary>
    Inline,
    /// <summary>Write the current definition to a loose file beside the state.</summary>
    Loose,
    /// <summary>Retain an unchanged durable source; otherwise embed the current definition.</summary>
    PreserveSource
}

/// <summary>Persistence choices for an EngineState save.</summary>
public sealed class EngineStateSaveOptions
{
    /// <summary>Whether to compress the state with GZip.</summary>
    public bool Compress { get; set; }
    /// <summary>The state parts to capture, including their dependencies.</summary>
    public EngineStateParts Parts { get; set; } = EngineStateParts.All;
    /// <summary>How to persist tilesheet definitions.</summary>
    public DefinitionPersistence Tilesheets { get; set; }
    /// <summary>How to persist animation definitions.</summary>
    public DefinitionPersistence Cycles { get; set; }
    /// <summary>How to persist scene definitions.</summary>
    public DefinitionPersistence Scenes { get; set; }
    /// <summary>How to persist audio definitions.</summary>
    public DefinitionPersistence Audio { get; set; }
    /// <summary>How to persist the sprite collection.</summary>
    public DefinitionPersistence Sprites { get; set; }
}
