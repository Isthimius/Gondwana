namespace Gondwana.Scenes.GSCN;

/// <summary>
/// Defines one named scene collision profile using group names rather than runtime masks.
/// </summary>
public sealed class SceneCollisionProfileDefinition
{
    /// <summary>
    /// Gets or sets the profile name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collision group assigned to the profile.
    /// </summary>
    public string CollisionGroup { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets collision groups with which this profile interacts.
    /// </summary>
    public List<string> CollidesWith { get; set; } = [];

    /// <summary>
    /// Gets or sets whether this profile interacts with all collision groups.
    /// </summary>
    public bool CollidesWithAll { get; set; }
}
