namespace Gondwana.Collisions;

/// <summary>
/// Manages collision group definitions and provides bitwise mask values for collision filtering.
/// Groups are represented as bit flags to allow efficient collision detection using bitwise operations.
/// </summary>
public sealed class CollisionGroupRegistry
{
    private readonly Dictionary<string, int> _groups = new(StringComparer.OrdinalIgnoreCase);
    private int _nextBit;

    /// <summary>
    /// Initializes a new instance of the <see cref="CollisionGroupRegistry"/> class with
    /// predefined collision groups: WorldStatic, Actors, Projectiles, and Triggers.
    /// </summary>
    public CollisionGroupRegistry()
    {
        Define("WorldStatic");
        Define("Actors");
        Define("Projectiles");
        Define("Triggers");
    }

    /// <summary>
    /// Defines a new collision group with the specified name, or returns the existing group value if already defined.
    /// Each group is assigned a unique bit flag value.
    /// </summary>
    /// <param name="name">The name of the collision group to define. Must not be empty or whitespace.</param>
    /// <returns>The bit flag value representing the collision group.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <c>null</c>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the maximum number of 31 collision groups has been exceeded.</exception>
    public int Define(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Group name cannot be empty.", nameof(name));

        if (_groups.TryGetValue(name, out var existing))
            return existing;

        // Allow 31 groups (0..30) to avoid using the sign bit.
        if (_nextBit >= 31)
            throw new InvalidOperationException("Max 31 collision groups exceeded for int masks.");

        int value = 1 << _nextBit++;
        _groups.Add(name, value);
        return value;
    }

    /// <summary>
    /// Gets the bit flag value for a previously defined collision group.
    /// </summary>
    /// <param name="name">The name of the collision group to retrieve.</param>
    /// <returns>The bit flag value representing the collision group.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the collision group with the specified <paramref name="name"/> has not been defined.</exception>
    public int Get(string name)
    {
        if (!_groups.TryGetValue(name, out var value))
            throw new KeyNotFoundException($"Collision group '{name}' not defined.");

        return value;
    }

    /// <summary>
    /// Gets a read-only collection of all defined collision group names.
    /// </summary>
    /// <returns>A collection containing the names of all registered collision groups.</returns>
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
