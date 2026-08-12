using Newtonsoft.Json;

namespace Gondwana.Physics.Collisions;

/// <summary>
/// Manages collision group definitions and provides bitwise mask values for collision filtering.
/// Groups are represented as bit flags to allow efficient collision detection using bitwise operations.
/// </summary>
public sealed class CollisionGroupRegistry
{
    [JsonProperty("groups")]
    private readonly Dictionary<string, int> _groups;

    [JsonProperty("nextBit")]
    private int _nextBit;

    /// <summary>
    /// Initializes a new instance of the <see cref="CollisionGroupRegistry"/> class with
    /// predefined collision groups: WorldStatic, Actors, Projectiles, and Triggers.
    /// </summary>
    public CollisionGroupRegistry()
    {
        _groups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        Define("WorldStatic");
        Define("Actors");
        Define("Projectiles");
        Define("Triggers");
    }

    /// <summary>
    /// JSON constructor used during deserialization to restore internal state.
    /// </summary>
    /// <param name="groups">The serialized group dictionary.</param>
    /// <param name="nextBit">The next available bit index.</param>
    [JsonConstructor]
    private CollisionGroupRegistry(Dictionary<string, int> groups, int nextBit)
    {
        _groups = new Dictionary<string, int>(groups ?? throw new ArgumentNullException(nameof(groups)),
                                              StringComparer.OrdinalIgnoreCase);
        _nextBit = nextBit;
    }

    /// <summary>
    /// Defines a new collision group with the specified name, or returns the existing group value if already defined.
    /// Each group is assigned a unique bit flag value.
    /// </summary>
    public int Define(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Group name cannot be empty.", nameof(name));

        if (_groups.TryGetValue(name, out var existing))
            return existing;

        if (_nextBit >= 31)
            throw new InvalidOperationException("Max 31 collision groups exceeded for int masks.");

        int value = 1 << _nextBit++;
        _groups.Add(name, value);
        return value;
    }

    /// <summary>
    /// Gets the bit flag value for a previously defined collision group.
    /// </summary>
    public int Get(string name)
    {
        if (!_groups.TryGetValue(name, out var value))
            throw new KeyNotFoundException($"Collision group '{name}' not defined.");

        return value;
    }

    /// <summary>
    /// Combines previously defined collision groups into one bit mask.
    /// An empty collection resolves to <see cref="CollisionMasks.None"/>.
    /// </summary>
    public int GetMask(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        int mask = CollisionMasks.None;
        foreach (var name in names)
            mask |= Get(name);

        return mask;
    }

    /// <summary>
    /// Gets a read-only collection of all defined collision group names.
    /// </summary>
    public IReadOnlyCollection<string> GetGroupNames() => _groups.Keys.ToArray();

    /// <summary>
    /// Gets the bit flag value for the WorldStatic collision group.
    /// </summary>
    public int WorldStatic => _groups["WorldStatic"];

    /// <summary>
    /// Gets the bit flag value for the Actors collision group.
    /// </summary>
    public int Actors => _groups["Actors"];

    /// <summary>
    /// Gets the bit flag value for the Projectiles collision group.
    /// </summary>
    public int Projectiles => _groups["Projectiles"];

    /// <summary>
    /// Gets the bit flag value for the Triggers collision group.
    /// </summary>
    public int Triggers => _groups["Triggers"];
}
