using System.Numerics;
using Gondwana.Drawing.Sprites;

namespace Gondwana.ZeldaPrototype;

internal enum GameMode
{
    /// <summary>
    /// Specifies title.
    /// </summary>
    Title,
    /// <summary>
    /// Specifies playing.
    /// </summary>
    Playing,
    /// <summary>
    /// Specifies dialogue.
    /// </summary>
    Dialogue,
    /// <summary>
    /// Specifies inventory.
    /// </summary>
    Inventory,
    /// <summary>
    /// Specifies paused.
    /// </summary>
    Paused,
    /// <summary>
    /// Specifies game over.
    /// </summary>
    GameOver,
    /// <summary>
    /// Specifies victory.
    /// </summary>
    Victory
}

internal enum WorldArea
{
    /// <summary>
    /// Specifies overworld.
    /// </summary>
    Overworld,
    /// <summary>
    /// Specifies dungeon.
    /// </summary>
    Dungeon
}

internal enum Facing
{
    /// <summary>
    /// Specifies up.
    /// </summary>
    Up,
    /// <summary>
    /// Specifies down.
    /// </summary>
    Down,
    /// <summary>
    /// Specifies left.
    /// </summary>
    Left,
    /// <summary>
    /// Specifies right.
    /// </summary>
    Right
}

internal enum InventoryItem
{
    /// <summary>
    /// Specifies sword.
    /// </summary>
    Sword,
    /// <summary>
    /// Specifies rusted key.
    /// </summary>
    RustedKey,
    /// <summary>
    /// Specifies potion.
    /// </summary>
    Potion,
    /// <summary>
    /// Specifies sun relic.
    /// </summary>
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
