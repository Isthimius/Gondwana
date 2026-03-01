namespace Gondwana.Collisions;

public sealed class CollisionGroupRegistry
{
    private readonly Dictionary<string, int> _groups = new(StringComparer.OrdinalIgnoreCase);
    private int _nextBit;

    public CollisionGroupRegistry()
    {
        Define("WorldStatic");
        Define("Actors");
        Define("Projectiles");
        Define("Triggers");
    }

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

    public int Get(string name)
    {
        if (!_groups.TryGetValue(name, out var value))
            throw new KeyNotFoundException($"Collision group '{name}' not defined.");

        return value;
    }

    public IReadOnlyCollection<string> GetGroupNames() => _groups.Keys.ToArray();

    public int WorldStatic => _groups["WorldStatic"];

    public int Actors => _groups["Actors"];

    public int Projectiles => _groups["Projectiles"];

    public int Triggers => _groups["Triggers"];
}
