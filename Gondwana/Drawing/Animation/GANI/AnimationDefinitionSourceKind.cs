namespace Gondwana.Drawing.Animation.GANI;

/// <summary>
/// Identifies where a GANI animation definition originated.
/// </summary>
public enum AnimationDefinitionSourceKind
{
    /// <summary>
    /// No source or option is selected.
    /// </summary>
    None = 0,
    /// <summary>
    /// The definition is stored in a loose file.
    /// </summary>
    LooseDefinitionFile = 1,
    /// <summary>
    /// The definition is stored in an assets file.
    /// </summary>
    PackedDefinitionFile = 2,
    /// <summary>
    /// The definition was generated from runtime state.
    /// </summary>
    Generated = 3
}
