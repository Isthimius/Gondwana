using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gondwana.ZeldaPrototype;

internal sealed record EnemySave(string Id, int Health);

internal sealed record SaveGame(
    int Version,
    float PlayerX,
    float PlayerY,
    WorldArea Area,
    Facing Facing,
    int Health,
    Dictionary<InventoryItem, int> Inventory,
    HashSet<string> CollectedPickups,
    List<EnemySave> Enemies);

internal static class SaveGameService
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    internal static string SavePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HiddenWorldsGames",
        "GondwanaZeldaPrototype",
        "savegame.json");

    internal static bool Exists => File.Exists(SavePath);

    internal static SaveGame Create(
        float playerX,
        float playerY,
        WorldArea area,
        Facing facing,
        int health,
        Dictionary<InventoryItem, int> inventory,
        HashSet<string> collectedPickups,
        IEnumerable<EnemyState> enemies)
    {
        return new SaveGame(
            CurrentVersion,
            playerX,
            playerY,
            area,
            facing,
            health,
            new Dictionary<InventoryItem, int>(inventory),
            new HashSet<string>(collectedPickups, StringComparer.Ordinal),
            enemies.Select(enemy => new EnemySave(enemy.Id, enemy.Health)).ToList());
    }

    internal static void Save(SaveGame save)
    {
        string directory = Path.GetDirectoryName(SavePath)
            ?? throw new InvalidOperationException("The save-game directory could not be resolved.");

        Directory.CreateDirectory(directory);

        string temporaryPath = SavePath + ".tmp";
        string json = JsonSerializer.Serialize(save, Options);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, SavePath, overwrite: true);
    }

    internal static SaveGame Load()
    {
        string json = File.ReadAllText(SavePath);
        SaveGame save = JsonSerializer.Deserialize<SaveGame>(json, Options)
            ?? throw new InvalidDataException("The save file did not contain a valid game state.");

        if (save.Version != CurrentVersion)
        {
            throw new InvalidDataException(
                $"Save version {save.Version} is not supported by this prototype.");
        }

        return save;
    }
}
