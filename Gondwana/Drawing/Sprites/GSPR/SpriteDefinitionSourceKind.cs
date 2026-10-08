namespace Gondwana.Drawing.Sprites.GSPR;

/// <summary>
/// Indicates where a scene definition came from.
/// </summary>
public enum SpriteDefinitionSourceKind
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
