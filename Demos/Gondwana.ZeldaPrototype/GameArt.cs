using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Tilesheets;

namespace Gondwana.ZeldaPrototype;

internal static class GameArt
{
    // Change only this value to switch the entire demo's art source.
    internal static readonly GameArtMode Mode = GameArtMode.Generated;

    internal const int TileSize = 32;

    internal const int Grass = 0;
    internal const int Path = 1;
    internal const int Water = 2;
    internal const int Tree = 3;
    internal const int Rock = 4;
    internal const int DungeonFloor = 5;
    internal const int DungeonWall = 6;
    internal const int Entrance = 7;
    internal const int PlayerUp = 8;
    internal const int PlayerDown = 9;
    internal const int PlayerLeft = 10;
    internal const int PlayerRight = 11;
    internal const int Slime = 12;
    internal const int Bat = 13;
    internal const int Elder = 14;
    internal const int SwordUp = 15;
    internal const int SwordDown = 16;
    internal const int SwordLeft = 17;
    internal const int SwordRight = 18;
    internal const int Potion = 19;
    internal const int Key = 20;
    internal const int Boss = 21;
    internal const int Gate = 22;
    internal const int Relic = 23;
    internal const int Flower = 24;

    private static readonly Dictionary<int, Frame> Frames = [];

    internal static void Load(TilesheetRegistry registry, string assetsDirectory)
    {
        Frames.Clear();

        if (Mode == GameArtMode.Generated)
        {
            LoadGenerated(registry);
            return;
        }

        LoadGts(registry, assetsDirectory);
    }

    private static void LoadGenerated(TilesheetRegistry registry)
    {
        Tilesheet tilesheet = registry.LoadFromBitmap(
            "greenward-generated-art",
            ProceduralGameArt.CreateTilesheetBitmap());

        tilesheet.DefaultRegion.TileSize = new Size(TileSize, TileSize);

        for (int id = Grass; id <= Flower; id++)
            Frames.Add(id, tilesheet[id, 0]);
    }

    private static void LoadGts(TilesheetRegistry registry, string assetsDirectory)
    {

        Tilesheet environment = Load(registry, assetsDirectory, "forest.gts");
        Tilesheet link = Load(registry, assetsDirectory, "link.gts");
        Tilesheet npcs = Load(registry, assetsDirectory, "npcs.gts");
        Tilesheet enemies = Load(registry, assetsDirectory, "lightworld_enemies.gts");
        Tilesheet ganon = Load(registry, assetsDirectory, "ganon.gts");

        Add(environment, Grass, "grass");
        Add(environment, Path, "path");
        Add(environment, Water, "water");
        Add(environment, Tree, "tree");
        Add(environment, Rock, "rock");
        Add(environment, DungeonFloor, "dungeon-floor");
        Add(environment, DungeonWall, "dungeon-wall");
        Add(environment, Entrance, "entrance");
        Add(environment, Gate, "gate");
        Add(environment, Relic, "relic");
        Add(environment, Flower, "flower");

        Add(link, PlayerUp, "player-up");
        Add(link, PlayerDown, "player-down");
        Add(link, PlayerLeft, "player-left");
        Add(link, PlayerRight, "player-right");
        Add(link, SwordUp, "sword-up");
        Add(link, SwordDown, "sword-down");
        Add(link, SwordLeft, "sword-left");
        Add(link, SwordRight, "sword-right");
        Add(link, Potion, "potion");
        Add(link, Key, "key");

        Add(npcs, Elder, "elder");
        Add(enemies, Slime, "slime");
        Add(enemies, Bat, "bat");
        Add(ganon, Boss, "boss");
    }

    internal static Frame GetFrame(int id) => Frames.TryGetValue(id, out Frame frame)
        ? frame
        : throw new KeyNotFoundException($"No {Mode} art frame is mapped for id {id}.");

    private static Tilesheet Load(
        TilesheetRegistry registry,
        string assetsDirectory,
        string definitionFileName) =>
        registry.LoadFromDefinitionFile(System.IO.Path.Combine(assetsDirectory, definitionFileName));

    private static void Add(Tilesheet tilesheet, int id, string regionName) =>
        Frames.Add(id, tilesheet[regionName, 0, 0]);
}

internal enum GameArtMode
{
    Generated,
    Gts
}
