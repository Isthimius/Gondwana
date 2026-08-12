using Newtonsoft.Json;

namespace Gondwana.Physics.Collisions;

/// <summary>
/// Stores named collision-filtering profiles for a scene. Profiles resolve their
/// group names through the scene's <see cref="CollisionGroupRegistry"/>.
/// </summary>
public sealed class CollisionProfileRegistry
{
    [JsonProperty("profiles")]
    private readonly Dictionary<string, CollisionProfile> _profiles;

    /// <summary>
    /// Initializes a registry with Gondwana's standard collision profiles.
    /// </summary>
    public CollisionProfileRegistry()
    {
        _profiles = new Dictionary<string, CollisionProfile>(StringComparer.OrdinalIgnoreCase);
        EnsureStandardProfiles();
    }

    [JsonConstructor]
    private CollisionProfileRegistry(Dictionary<string, CollisionProfile>? profiles)
    {
        _profiles = new Dictionary<string, CollisionProfile>(
            profiles ?? new Dictionary<string, CollisionProfile>(),
            StringComparer.OrdinalIgnoreCase);

        EnsureStandardProfiles();
    }

    /// <summary>
    /// Defines or replaces a named profile.
    /// </summary>
    public CollisionProfile Define(
        string name,
        string collisionGroup,
        IEnumerable<string>? collidesWith = null,
        bool collidesWithAll = false)
    {
        var profile = new CollisionProfile(
            name,
            collisionGroup,
            collidesWith,
            collidesWithAll);

        _profiles[name] = profile;
        return profile;
    }

    /// <summary>
    /// Gets a previously defined profile.
    /// </summary>
    public CollisionProfile Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collision profile name cannot be empty.", nameof(name));

        if (!_profiles.TryGetValue(name, out var profile))
            throw new KeyNotFoundException($"Collision profile '{name}' is not defined.");

        return profile;
    }

    /// <summary>
    /// Attempts to get a previously defined profile.
    /// </summary>
    public bool TryGet(string name, out CollisionProfile? profile)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            profile = null;
            return false;
        }

        return _profiles.TryGetValue(name, out profile);
    }

    /// <summary>
    /// Gets the names of all defined profiles.
    /// </summary>
    public IReadOnlyCollection<string> GetProfileNames() => _profiles.Keys.ToArray();

    private void EnsureStandardProfiles()
    {
        DefineIfMissing(
            CollisionProfileNames.World,
            "WorldStatic",
            ["Actors", "Projectiles"]);

        DefineIfMissing(
            CollisionProfileNames.Actor,
            "Actors",
            ["WorldStatic", "Actors", "Projectiles", "Triggers"]);

        DefineIfMissing(
            CollisionProfileNames.Projectile,
            "Projectiles",
            ["WorldStatic", "Actors"]);

        DefineIfMissing(
            CollisionProfileNames.Sensor,
            "Triggers",
            ["Actors"]);
    }

    private void DefineIfMissing(
        string name,
        string collisionGroup,
        IEnumerable<string> collidesWith)
    {
        if (!_profiles.ContainsKey(name))
            Define(name, collisionGroup, collidesWith);
    }
}
