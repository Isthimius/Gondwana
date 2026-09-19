namespace Gondwana.Drawing.Animation.GANI;

/// <summary>
/// Identifies where a GANI animation definition originated.
/// </summary>
public enum AnimationDefinitionSourceKind
{
    None = 0,
    LooseDefinitionFile = 1,
    PackedDefinitionFile = 2,
    Generated = 3
}
