using System.Numerics;
using Gondwana.Demos.Adventure;

namespace Adventure.Tests;

/// <summary>
/// Checks the Game 2 puzzle topology using player-sized collision geometry,
/// not merely adjacency between room identifiers.
/// </summary>
public sealed class AdventureQuestGeometryTests
{
    private const int Step = 12;

    private static readonly (AdventureWorld.Direction Direction, Vector2 Delta)[] Moves =
    [
        (AdventureWorld.Direction.North, new(0, -Step)),
        (AdventureWorld.Direction.East, new(Step, 0)),
        (AdventureWorld.Direction.South, new(0, Step)),
        (AdventureWorld.Direction.West, new(-Step, 0))
    ];

    [Fact]
    public void OutdoorQuestToolsAndAllCastleGatesAreReachable()
    {
        var game = new AdventureGame(1);
        var reachable = Explore(AdventureWorld.GoldCastle, AdventureWorld.CastleApproach, allowBridge: false);

        foreach (var kind in new[]
        {
            AdventureGame.ItemKind.GoldKey,
            AdventureGame.ItemKind.WhiteKey,
            AdventureGame.ItemKind.Bridge,
            AdventureGame.ItemKind.Magnet,
            AdventureGame.ItemKind.Sword
        })
        {
            Assert.True(CanTouch(reachable, game.Items.Single(item => item.Kind == kind)), kind.ToString());
        }

        foreach (int castle in new[] { 15, 16, 17 })
            Assert.Contains(reachable, p => p.Room == castle &&
                AdventureWorld.IsWithin(p.Position, AdventureWorld.Gate, 24f));
    }

    [Fact]
    public void WhiteCastleBlackKeyRequiresBridge()
    {
        var game = new AdventureGame(1);
        var key = game.Items.Single(item => item.Kind == AdventureGame.ItemKind.BlackKey);
        int keep = AdventureWorld.Interior(15);

        Assert.False(CanTouch(Explore(keep, AdventureWorld.KeepEntry, allowBridge: false), key));
        Assert.True(CanTouch(Explore(keep, AdventureWorld.KeepEntry, allowBridge: true), key));
    }

    [Fact]
    public void BlackCastleChaliceIsReachableButDotRequiresBridge()
    {
        var game = new AdventureGame(1);
        var chalice = game.Items.Single(item => item.Kind == AdventureGame.ItemKind.Chalice);
        var dot = game.Items.Single(item => item.Kind == AdventureGame.ItemKind.Dot);
        int keep = AdventureWorld.Interior(16);
        var walking = Explore(keep, AdventureWorld.KeepEntry, allowBridge: false);

        Assert.True(CanTouch(walking, chalice));
        Assert.False(CanTouch(walking, dot));
        Assert.True(CanTouch(Explore(keep, AdventureWorld.KeepEntry, allowBridge: true), dot));
    }

    private static bool CanTouch(HashSet<(int Room, Vector2 Position)> reachable, AdventureGame.Item item)
        => reachable.Any(p => p.Room == item.Room && Vector2.DistanceSquared(p.Position, item.Position) < 26f * 26f);

    // A coarse geometry search, not an AI playthrough. Gates are not traversed;
    // dungeon searches begin at the corresponding unlocked keep. A bridge move
    // represents laying a bridge at the near end of a vertical wall and crossing it.
    private static HashSet<(int Room, Vector2 Position)> Explore(int initialRoom, Vector2 initialPosition, bool allowBridge)
    {
        var pending = new Queue<(int Room, Vector2 Position)>();
        var seen = new HashSet<(int Room, Vector2 Position)>();
        pending.Enqueue((initialRoom, Snap(initialPosition)));

        while (pending.TryDequeue(out var state))
        {
            if (AdventureWorld.IsBlocked(state.Room, state.Position) || !seen.Add(state))
                continue;

            foreach (var (direction, delta) in Moves)
            {
                Vector2 next = state.Position + delta;

                if (AdventureWorld.IsBlocked(state.Room, next))
                {
                    if (!allowBridge || delta.X != 0f || next.Y < 60f || next.Y > AdventureWorld.Floor - 60f)
                        continue;

                    while (AdventureWorld.IsBlocked(state.Room, next) &&
                           AdventureWorld.IsBridgePassage(state.Position, next))
                        next += delta;

                    if (!AdventureWorld.IsBridgePassage(state.Position, next) ||
                        next.Y < 60f || next.Y > AdventureWorld.Floor - 60f ||
                        AdventureWorld.IsBlocked(state.Room, next))
                        continue;
                }

                int room = state.Room;

                if (next.X < 0 || next.X > AdventureWorld.Width || next.Y < 0 || next.Y > AdventureWorld.Floor)
                {
                    room = AdventureWorld.GetRoom(room).Exit(direction);

                    if (room == state.Room || AdventureWorld.IsCastle(room) && AdventureWorld.Exterior(initialRoom) != 0)
                        continue;

                    Vector2 entry = direction switch
                    {
                        AdventureWorld.Direction.North => new(next.X, AdventureWorld.Floor - 18),
                        AdventureWorld.Direction.East => new(18, next.Y),
                        AdventureWorld.Direction.South => new(next.X, 18),
                        _ => new(AdventureWorld.Width - 18, next.Y)
                    };
                    next = Snap(AdventureWorld.FindOpenPosition(room, entry));
                }

                if (!seen.Contains((room, next)))
                    pending.Enqueue((room, next));
            }
        }

        return seen;
    }

    private static Vector2 Snap(Vector2 position)
        => new(MathF.Round(position.X / Step) * Step, MathF.Round(position.Y / Step) * Step);
}
