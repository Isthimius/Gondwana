using System.Drawing;
using System.Numerics;

namespace Gondwana.Demos.Adventure;

/// <summary>Level-two kingdom topology and playfield geometry, expressed in logical pixels.</summary>
internal static class AdventureWorld
{
    internal const int Width = 960;
    internal const int Floor = 720;
    internal const int Height = Floor + 64;
    internal const int PlayerRadius = 10;
    internal const int GoldCastle = 17;
    internal const int GoldInterior = 18;
    internal const uint BackgroundColor = 0xffb0b0b0;
    internal static readonly RectangleF SecretWall = new(Width - 6, 0, 6, Floor);

    internal enum Direction { North, East, South, West }
    private enum ThinWall { None, Left, Right }

    internal sealed record Room(int Id, string Name, int North, int East, int South, int West,
        uint Color, RectangleF[] Geometry, bool Dark = false)
    {
        internal int Exit(Direction direction) => direction switch
        {
            Direction.North => North,
            Direction.East => East,
            Direction.South => South,
            _ => West
        };
    }

    // Geometric interpretation of the documented Atari playfields. Each row
    // describes forty horizontal cells; the end bands are half-height.
    // The source room links are Up, Left, Down, Right, not clockwise.
    // See the reference links in README.md. No original executable code is used.
    private static readonly string[] BelowCastle =
    [
        "################........################",
        "........................................",
        "........................................",
        "........................................",
        "........................................",
        "........................................",
        "########################################"
    ];

    private static readonly string[] HiddenPassage =
    [
        "########################################",
        "........................................",
        "........................................",
        "........................................",
        "........................................",
        "........................................",
        "################........################"
    ];

    private static readonly string[] SideCorridor =
    [
        "################........################",
        "........................................",
        "........................................",
        "........................................",
        "........................................",
        "........................................",
        "################........################"
    ];

    private static readonly string[] EnclosedKeep =
    [
        "########################################",
        "##....................................##",
        "##....................................##",
        "##....................................##",
        "##....................................##",
        "##....................................##",
        "################........################"
    ];

    private static readonly string[] TwoExitKeep =
    [
        "################........################",
        "##....................................##",
        "##....................................##",
        "##....................................##",
        "##....................................##",
        "##....................................##",
        "################........################"
    ];

    private static readonly string[] Castle =
    [
        "###########.#.#.#......#.#.#.###########",
        "##........#######......#######........##",
        "##........####################........##",
        "##..........################..........##",
        "##..........######....######..........##",
        "##....................................##",
        "################........################"
    ];

    private static readonly string[] BlueNorth =
    [
        "################........################",
        "........##....##........##....##........",
        "####....##....####....####....##....####",
        "####....##....................##....####",
        "##################....##################",
        "......##........##....##........##......",
        "####..##..########....########..##..####"
    ];

    private static readonly string[] BlueWest =
    [
        "########################################",
        "........................................",
        "##########..################..##########",
        "####..............####..............####",
        "####..##########..####..##########..####",
        "......##......##..####..##......##......",
        "########..##..##..####..##..##..########"
    ];

    private static readonly string[] BlueSouth =
    [
        "########..##..##........##..##..########",
        "......##......##........##......##......",
        "####..##########........##########..####",
        "####................................####",
        "########........................########",
        "......##........................##......",
        "########################################"
    ];

    private static readonly string[] BlueCenter =
    [
        "####..##..########....########..##..####",
        "......##......####....####......##......",
        "############..####....####..############",
        "..........##..####....####..##..........",
        "####..##..##..####....####..##..##..####",
        "......##..##..##........##..##..##......",
        "########..##..##........##..##..########"
    ];

    private static readonly string[] BlueEntry =
    [
        "########..##..##..####..##..##..########",
        "......##..##..##........##..##..##......",
        "####..##..##..############..##..##..####",
        "......##..##................##..##......",
        "########..####################..########",
        "........................................",
        "################........################"
    ];

    private static readonly string[] DarkCenter =
    [
        "############..##..####..##..############",
        "..............##..####..##..............",
        "####......######..####..######......####",
        "..........##................##..........",
        "########..##..############..##..########",
        "......##..##..##........##..##..##......",
        "####..##..##..##..####..##..##..##..####"
    ];

    private static readonly string[] DarkEntry =
    [
        "################........################",
        "......##........................##......",
        "####..##....################....##..####",
        "......##..........####..........##......",
        "########..##......####......##..########",
        "..........##......####......##..........",
        "############..##..####..##..############"
    ];

    private static readonly string[] DarkSide =
    [
        "####..##..##..##..####..##..##..##..####",
        "......##......##..####..##......##......",
        "......##########..####..##########......",
        "..................####..................",
        "......########....####....########......",
        "......##..........####..........##......",
        "########################################"
    ];

    private static readonly string[] TopEntry =
    [
        "################........################",
        "##....................................##",
        "##....................................##",
        "##....................................##",
        "##....................................##",
        "##....................................##",
        "########################################"
    ];

    private static readonly string[] BlackOne =
    [
        "########....################....########",
        "............##............##............",
        "##############............##############",
        "........................................",
        "##....############################....##",
        "......##........................##......",
        "########....################....########"
    ];

    private static readonly string[] BlackTwo =
    [
        "########################################",
        "..................##..................##",
        "################..##################..##",
        "..............##..................##....",
        "####....################....############",
        "........####......##........####......##",
        "##..##..####..##..####..##..####..##..##"
    ];

    private static readonly string[] BlackThree =
    [
        "########....################....########",
        "##..................##..................",
        "##....################....##############",
        "......##..................##............",
        "########....################....########",
        "##..........##......##..........##......",
        "########....################....########"
    ];

    private static readonly string[] BlackEntry =
    [
        "##..##..####..##..####..##..####..##..##",
        "....##........##..####..##........##....",
        "################........################",
        "........................................",
        "################........################",
        "........................................",
        "################........################"
    ];

    private static readonly string[] RedOne =
    [
        "########################################",
        "........................................",
        "################........################",
        "..............##........##..............",
        "############..##........##..############",
        "####......##..##..####..##..##......####",
        "####..##..######..####..######..##..####"
    ];

    private static readonly string[] RedNorth =
    [
        "########################################",
        "..................####..................",
        "################..####..################",
        "..............##..####..##..............",
        "####..##..####################..##..####",
        "####..##..##................##..##..####",
        "####..######..##........##..######..####"
    ];

    private static readonly string[] RedSouth =
    [
        "####..##..######..####..######..##..####",
        "####..##........................##..####",
        "####..##..####################..##..####",
        "......##..##................##..##......",
        "############................############",
        "........................................",
        "################........################"
    ];

    private static readonly string[] RedEntry =
    [
        "####..######..##........##..######..####",
        "####..........##........##..........####",
        "################........################",
        "......##........................##......",
        "####..##........................##..####",
        "......##........................##......",
        "################........################"
    ];

    internal static readonly Room[] Rooms =
    [
        new(1, "Olive passage", 8, 3, 15, 2, 0xff9ca864, BuildWalls(BelowCastle, ThinWall.Left)),
        new(2, "Green passage", 17, 1, 17, 3, 0xff84b468, BuildWalls(BelowCastle)),
        new(3, "Hidden passage", 6, 2, 10, 1, 0xffb89c58, BuildWalls(HiddenPassage, ThinWall.Right)),
        new(4, "Blue labyrinth north", 16, 6, 7, 5, 0xff505cc0, BuildWalls(BlueNorth)),
        new(5, "Blue labyrinth west", 29, 4, 8, 6, 0xff505cc0, BuildWalls(BlueWest)),
        new(6, "Blue labyrinth south", 7, 5, 3, 4, 0xff505cc0, BuildWalls(BlueSouth)),
        new(7, "Blue labyrinth center", 4, 8, 6, 8, 0xff505cc0, BuildWalls(BlueCenter)),
        new(8, "Blue labyrinth entrance", 5, 7, 1, 7, 0xff505cc0, BuildWalls(BlueEntry)),
        new(9, "Dark labyrinth center", 10, 10, 11, 10, 0xffb0b0b0, BuildWalls(DarkCenter, ThinWall.None), true),
        new(10, "Dark labyrinth entrance", 3, 9, 9, 9, 0xffb0b0b0, BuildWalls(DarkEntry, ThinWall.None), true),
        new(11, "Dark labyrinth side", 9, 13, 28, 12, 0xffb0b0b0, BuildWalls(DarkSide, ThinWall.None), true),
        new(12, "Blue-green passage", 28, 11, 29, 13, 0xff6888cc, BuildWalls(SideCorridor, ThinWall.Right)),
        new(13, "Green side passage", 15, 12, 14, 11, 0xff68b494, BuildWalls(SideCorridor, ThinWall.Left)),
        new(14, "White castle approach", 13, 16, 15, 16, 0xff689cc0, BuildWalls(TopEntry)),
        new(15, "White castle", 14, 15, 13, 15, 0xffdcdcdc, BuildWalls(Castle)),
        new(16, "Black castle", 1, 28, 4, 28, 0xff000000, BuildWalls(Castle)),
        new(17, "Gold castle", 6, 1, 2, 3, 0xffd0d050, BuildWalls(Castle)),
        new(18, "Gold castle keep", 18, 18, 17, 18, 0xffd0d050, BuildWalls(EnclosedKeep)),
        new(19, "Black dungeon I", 21, 22, 21, 20, 0xffb0b0b0, BuildWalls(BlackOne, ThinWall.None), true),
        new(20, "Black dungeon II", 22, 19, 22, 21, 0xffb0b0b0, BuildWalls(BlackTwo, ThinWall.None), true),
        new(21, "Black dungeon III", 19, 20, 19, 22, 0xffb0b0b0, BuildWalls(BlackThree, ThinWall.None), true),
        new(22, "Black dungeon entrance", 20, 21, 27, 19, 0xffb0b0b0, BuildWalls(BlackEntry, ThinWall.None), true),
        new(23, "Red dungeon I", 25, 24, 25, 24, 0xffc06848, BuildWalls(RedOne)),
        new(24, "Red dungeon north", 26, 23, 26, 23, 0xffc06848, BuildWalls(RedNorth)),
        new(25, "Red dungeon south", 23, 26, 23, 26, 0xffc06848, BuildWalls(RedSouth)),
        new(26, "White castle keep", 24, 25, 15, 25, 0xffc06848, BuildWalls(RedEntry)),
        new(27, "Black castle keep", 22, 22, 16, 22, 0xffc06848, BuildWalls(TwoExitKeep)),
        new(28, "Purple chamber", 29, 8, 12, 7, 0xff8c58b8, BuildWalls(EnclosedKeep)),
        new(29, "Southern approach", 12, 3, 16, 1, 0xffc06848, BuildWalls(TopEntry)),
        new(30, "Secret chamber", 6, 3, 6, 3, 0xff8c58b8, BuildWalls(BelowCastle))
    ];

    internal static Room GetRoom(int id) => Rooms[id - 1];

    internal static bool IsCastle(int room) => room is 15 or 16 or 17;

    internal static int Interior(int room) => room switch
    {
        15 => 26,
        16 => 27,
        17 => 18,
        _ => 0
    };

    internal static int Exterior(int room) => room switch
    {
        26 => 15,
        27 => 16,
        18 => 17,
        _ => 0
    };

    // The castle shape has a central doorway in its lower facade.
    internal static readonly RectangleF Gate = new(Width / 2f - 48, 498, 96, 42);
    internal static Vector2 CastleApproach => new(Width / 2f, 590);
    internal static Vector2 KeepEntry => new(Width / 2f, Floor - 25);

    private static RectangleF[] BuildWalls(string[] rows, ThinWall thin = ThinWall.None)
    {
        const int cellWidth = Width / 40;
        int[] edges = [0, 60, 180, 300, 420, 540, 660, Floor];
        var walls = new List<RectangleF>();

        for (int y = 0; y < rows.Length; y++)
        {
            int x = 0;

            while (x < 40)
            {
                if (rows[y][x] != '#')
                {
                    x++;
                    continue;
                }

                int start = x;

                while (x < 40 && rows[y][x] == '#')
                    x++;

                walls.Add(new RectangleF(start * cellWidth, edges[y],
                    (x - start) * cellWidth, edges[y + 1] - edges[y]));
            }
        }

        if (thin == ThinWall.Left)
            walls.Add(new RectangleF(0, 0, 6, Floor));
        else if (thin == ThinWall.Right)
            walls.Add(SecretWall);

        return walls.ToArray();
    }

    internal static IEnumerable<RectangleF> Walls(Room room, bool secretOpen = false)
    {
        foreach (RectangleF wall in room.Geometry)
        {
            if (room.Id == 3 && secretOpen && wall == SecretWall)
                continue;

            yield return wall;
        }
    }

    internal static bool IsWithin(Vector2 position, RectangleF rectangle, float radius = PlayerRadius)
        => rectangle.IntersectsWith(new RectangleF(position.X - radius, position.Y - radius, radius * 2, radius * 2));

    internal static bool IsBlocked(int room, Vector2 position, bool secretOpen = false, float radius = PlayerRadius)
        => Walls(GetRoom(room), secretOpen).Any(wall => IsWithin(position, wall, radius));

    // Choose an open floor position for actors/items rather than embedding them
    // in walls when a room layout or randomized placement changes.
    internal static Vector2 FindOpenPosition(int room, Vector2 preferred, float radius = PlayerRadius)
    {
        preferred = Vector2.Clamp(preferred, new Vector2(radius + 1), new Vector2(Width - radius - 1, Floor - radius - 1));

        if (!IsBlocked(room, preferred, radius: radius))
            return preferred;

        Vector2 best = preferred;
        float distance = float.PositiveInfinity;

        for (int y = 18; y < Floor - radius; y += 12)
        {
            for (int x = 18; x < Width - radius; x += 12)
            {
                var candidate = new Vector2(x, y);
                float d = Vector2.DistanceSquared(preferred, candidate);

                if (d >= distance || IsBlocked(room, candidate, radius: radius))
                    continue;

                best = candidate;
                distance = d;
            }
        }

        return best;
    }
}
