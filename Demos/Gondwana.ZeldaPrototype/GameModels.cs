using System.Numerics;
using Gondwana.Drawing.Sprites;

namespace Gondwana.ZeldaPrototype;

internal enum GameMode
{
    Title,
    Playing,
    Dialogue,
    Inventory,
    Paused,
    GameOver,
    Victory
}

internal enum WorldArea
{
    Overworld,
    Dungeon
}

internal enum Facing
{
    Up,
    Down,
    Left,
    Right
}

internal enum InventoryItem
{
    Sword,
    RustedKey,
    Potion,
    SunRelic
}

internal sealed class EnemyState
{
    internal EnemyState(
        string id,
        Sprite sprite,
        Vector2 spawnPosition,
        WorldArea area,
        int maximumHealth,
        float speed,
        int contactDamage,
        bool isBoss = false)
    {
        Id = id;
        Sprite = sprite;
        SpawnPosition = spawnPosition;
        Area = area;
        MaximumHealth = maximumHealth;
        Health = maximumHealth;
        Speed = speed;
        ContactDamage = contactDamage;
        IsBoss = isBoss;
    }

    internal string Id { get; }
    internal Sprite Sprite { get; }
    internal Vector2 SpawnPosition { get; }
    internal WorldArea Area { get; }
    internal int MaximumHealth { get; }
    internal int Health { get; set; }
    internal float Speed { get; }
    internal int ContactDamage { get; }
    internal bool IsBoss { get; }
    internal GameHealthBar HealthBar { get; set; } = null!;
    internal bool IsAlive => Health > 0;
}

internal sealed class PickupState
{
    internal PickupState(string id, InventoryItem item, int amount, Sprite sprite)
    {
        Id = id;
        Item = item;
        Amount = amount;
        Sprite = sprite;
    }

    internal string Id { get; }
    internal InventoryItem Item { get; }
    internal int Amount { get; }
    internal Sprite Sprite { get; }
}
