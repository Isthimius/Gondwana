using System.Drawing;
using System.Numerics;

namespace Gondwana.Demos.Adventure;

/// <summary>Room graph and independently authored room geometry for the retro adventure.</summary>
internal static class AdventureWorld
{
    internal const int Width = 1280;
    internal const int Height = 720;
    internal const int Floor = 656;
    internal const int PlayerRadius = 10;
    internal const int GoldCastle = 17;
    internal const int GoldInterior = 18;

    internal enum Direction { North, East, South, West }

    internal sealed record Room(int Id, string Name, int North, int East, int South, int West, uint Color, bool Dark = false)
    {
        internal int Exit(Direction direction) => direction switch
        {
            Direction.North => North,
            Direction.East => East,
            Direction.South => South,
            _ => West
        };
    }

    // The full kingdom's directional links are based on the documented level 2 room graph.
    // Castle portals and the secret entrance are handled separately from ordinary screen edges.
    internal static readonly Room[] Rooms =
    [
        new(1, "Olive passage", 8, 2, 15, 3, 0xff9d9b35),
        new(2, "Green passage", 17, 3, 17, 1, 0xff72ad37),
        new(3, "Hidden passage", 6, 1, 10, 2, 0xffcba448),
        new(4, "Blue labyrinth north", 16, 5, 7, 6, 0xff426aba),
        new(5, "Blue labyrinth west", 29, 6, 8, 4, 0xff426aba),
        new(6, "Blue labyrinth south", 7, 4, 3, 5, 0xff426aba),
        new(7, "Blue labyrinth center", 4, 8, 6, 8, 0xff426aba),
        new(8, "Blue labyrinth entrance", 5, 7, 1, 7, 0xff426aba),
        new(9, "Dark labyrinth center", 10, 10, 11, 10, 0xffb6b6b6, true),
        new(10, "Dark labyrinth entrance", 3, 9, 9, 9, 0xffb6b6b6, true),
        new(11, "Dark labyrinth side", 9, 12, 28, 13, 0xffb6b6b6, true),
        new(12, "Cyan passage", 28, 13, 29, 11, 0xff4eaaa2),
        new(13, "Violet passage", 15, 11, 14, 12, 0xffa475b5),
        new(14, "White castle approach", 13, 16, 15, 16, 0xff69a88b),
        new(15, "White castle", 14, 15, 13, 15, 0xffeeeeee),
        new(16, "Black castle", 1, 28, 4, 28, 0xff222222),
        new(17, "Gold castle", 6, 3, 2, 1, 0xffdfbd35),
        new(18, "Gold castle keep", 18, 18, 18, 18, 0xffdfbd35),
        new(19, "Black dungeon I", 21, 20, 21, 22, 0xffaaaaaa, true),
        new(20, "Black dungeon II", 22, 21, 22, 19, 0xffaaaaaa, true),
        new(21, "Black dungeon III", 19, 22, 19, 20, 0xffaaaaaa, true),
        new(22, "Black dungeon entrance", 20, 19, 27, 21, 0xffaaaaaa, true),
        new(23, "Red dungeon I", 25, 24, 25, 24, 0xffb3483d),
        new(24, "Red dungeon north", 26, 23, 26, 23, 0xffb3483d),
        new(25, "Red dungeon south", 23, 26, 23, 26, 0xffb3483d),
        new(26, "White castle keep", 24, 25, 24, 25, 0xffb3483d),
        new(27, "Black castle keep", 22, 22, 22, 22, 0xffb3483d),
        new(28, "Purple chamber", 29, 7, 12, 8, 0xff9b69b5),
        new(29, "Southern approach", 12, 1, 16, 3, 0xffb3483d),
        new(30, "Secret chamber", 6, 1, 6, 3, 0xff9b69b5)
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

    internal static readonly RectangleF Gate = new(Width / 2f - 48, 150, 96, 48);

    // All ordinary exits meet a generous central passage. Walls are authored in
    // logical world pixels and shared by the renderer and simulation.
    internal static RectangleF[] Walls(Room room)
    {
        var walls = new List<RectangleF>();

        if (IsCastle(room.Id))
        {
            walls.Add(new RectangleF(400, 68, 480, 42));
            walls.Add(new RectangleF(400, 110, 170, 130));
            walls.Add(new RectangleF(710, 110, 170, 130));
        }
        else if (room.Id is >= 4 and <= 11 or >= 19 and <= 25)
        {
            float left = 155 + room.Id % 3 * 38;
            float right = 855 - room.Id % 3 * 38;
            walls.Add(new RectangleF(left, 85, 42, 190));
            walls.Add(new RectangleF(left, 385, 42, 185));
            walls.Add(new RectangleF(right, 85, 42, 190));
            walls.Add(new RectangleF(right, 385, 42, 185));
            walls.Add(new RectangleF(left + 42, 210, 170, 34));
            walls.Add(new RectangleF(right - 170, 410, 170, 34));
        }

        return walls.ToArray();
    }

    internal static bool IsWithin(Vector2 position, RectangleF rectangle, float radius = PlayerRadius)
        => rectangle.IntersectsWith(new RectangleF(position.X - radius, position.Y - radius, radius * 2, radius * 2));
}
