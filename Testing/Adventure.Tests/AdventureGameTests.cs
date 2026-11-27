using System.Numerics;
using Gondwana.Demos.Adventure;

namespace Adventure.Tests;

public sealed class AdventureGameTests
{
    [Fact]
    public void AllThirtyRoomsHaveValidExitsAndAreReachable()
    {
        Assert.Equal(30, AdventureWorld.Rooms.Length);
        var seen = new HashSet<int>();
        var pending = new Queue<int>();
        pending.Enqueue(AdventureWorld.GoldCastle);

        while (pending.TryDequeue(out int id))
        {
            if (!seen.Add(id))
                continue;

            var room = AdventureWorld.GetRoom(id);

            foreach (var direction in Enum.GetValues<AdventureWorld.Direction>())
            {
                int next = room.Exit(direction);
                Assert.InRange(next, 1, 30);
                pending.Enqueue(next);
            }

            int interior = AdventureWorld.Interior(id);

            if (interior != 0)
                pending.Enqueue(interior);

            if (id == 3)
                pending.Enqueue(30);
        }

        Assert.Equal(30, seen.Count);
    }

    [Fact]
    public void StartPauseAndResetPreserveExpectedState()
    {
        var game = new AdventureGame(1);
        Assert.Equal(AdventureGame.Phase.Title, game.State);
        game.Start();
        Assert.Equal(AdventureGame.Phase.Playing, game.State);
        game.Update(0.05f, Vector2.UnitX);
        Assert.True(game.Player.X > AdventureWorld.Width / 2f);
        game.Pause();
        Vector2 position = game.Player;
        game.Update(0.05f, Vector2.UnitX);
        Assert.Equal(position, game.Player);
        game.Reset();
        Assert.Equal(AdventureGame.Phase.Title, game.State);
        Assert.Equal(AdventureWorld.GoldCastle, game.RoomId);
        Assert.Null(game.Held);
        Assert.Equal(8, game.Items.Count);
    }

    [Fact]
    public void GoldKeyCanOpenCastleAndTravelWithPlayer()
    {
        var game = new AdventureGame(1);
        game.Start();

        for (int i = 0; i < 20; i++)
            game.Update(0.05f, -Vector2.UnitX);

        Assert.Equal(AdventureGame.ItemKind.GoldKey, game.Held?.Kind);

        for (int i = 0; i < 20; i++)
            game.Update(0.05f, Vector2.UnitX);

        for (int i = 0; i < 12 && game.RoomId == AdventureWorld.GoldCastle; i++)
            game.Update(0.05f, -Vector2.UnitY);

        Assert.True(game.IsGateOpen(AdventureWorld.GoldCastle));
        Assert.Equal(AdventureWorld.GoldInterior, game.RoomId);
        Assert.Equal(game.RoomId, game.Held!.Room);
    }

    [Fact]
    public void PlayfieldsHaveDistinctMazeGeometryAndClosedBorders()
    {
        Assert.Equal(AdventureWorld.Width * 3, AdventureWorld.Floor * 4);
        var north = AdventureWorld.GetRoom(4);
        var south = AdventureWorld.GetRoom(6);
        Assert.False(north.Geometry.SequenceEqual(south.Geometry));
        Assert.True(AdventureWorld.IsBlocked(6, new Vector2(480, 710)));
        Assert.False(AdventureWorld.IsBlocked(1, new Vector2(480, 10)));
        Assert.True(AdventureWorld.IsBlocked(1, new Vector2(8, 360)));
        Assert.True(AdventureWorld.IsBlocked(3, new Vector2(954, 360)));
        Assert.False(AdventureWorld.IsBlocked(3, new Vector2(954, 360), secretOpen: true));
        Assert.Equal(3, AdventureWorld.GetRoom(1).East);
        Assert.Equal(2, AdventureWorld.GetRoom(1).West);
        Assert.Equal(10, AdventureWorld.GetRoom(3).South);
    }

    [Fact]
    public void ItemsAndDragonsStartOnOpenFloor()
    {
        var game = new AdventureGame(1);
        Assert.All(game.Items, item => Assert.False(AdventureWorld.IsBlocked(item.Room, item.Position)));
        Assert.All(game.Dragons, dragon => Assert.False(AdventureWorld.IsBlocked(dragon.Room, dragon.Position, radius: 20f)));
    }

    [Fact]
    public void ClosedCastleSideWallBlocksRoomTransition()
    {
        var game = new AdventureGame(1);
        game.Start();

        for (int i = 0; i < 70; i++)
            game.Update(0.05f, Vector2.UnitX);

        Assert.Equal(AdventureWorld.GoldCastle, game.RoomId);
        Assert.True(game.Player.X < AdventureWorld.Width - 48);
    }

    [Fact]
    public void BridgeSpansInteriorWallButNotRoomEdge()
    {
        var center = new Vector2(408, 408);
        var farSide = new Vector2(408, 550);
        Assert.True(AdventureWorld.IsBridgePassage(center, new Vector2(408, 480)));
        Assert.True(AdventureWorld.IsBridgePassage(center, farSide));
        Assert.False(AdventureWorld.IsBlocked(21, farSide));
        Assert.False(AdventureWorld.IsBridgePassage(center, new Vector2(450, 480)));
        Assert.False(AdventureWorld.IsBridgePassage(new Vector2(480, 80), new Vector2(480, 5)));
    }

    [Fact]
    public void SynthesizedEffectsContainValidPcmWaveHeaders()
    {
        foreach (var sound in Enum.GetValues<AdventureGame.Sound>())
        {
            using var stream = AdventureAudio.CreateWave(sound);
            using var reader = new BinaryReader(stream);
            Assert.Equal("RIFF", new string(reader.ReadChars(4)));
            Assert.Equal(stream.Length - 8, (long)reader.ReadInt32());
            Assert.Equal("WAVE", new string(reader.ReadChars(4)));
            Assert.Equal("fmt ", new string(reader.ReadChars(4)));
            Assert.Equal(16, reader.ReadInt32());
            Assert.Equal((short)1, reader.ReadInt16());
            Assert.Equal((short)1, reader.ReadInt16());
            Assert.Equal(22050, reader.ReadInt32());
            stream.Position = 36;
            Assert.Equal("data", new string(reader.ReadChars(4)));
            Assert.Equal(stream.Length - 44, (long)reader.ReadInt32());
        }
    }
}
