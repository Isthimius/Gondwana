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
        Assert.True(game.Player.X > 640f);
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

        for (int i = 0; i < 28; i++)
            game.Update(0.05f, -Vector2.UnitX);

        Assert.Equal(AdventureGame.ItemKind.GoldKey, game.Held?.Kind);

        for (int i = 0; i < 28; i++)
            game.Update(0.05f, Vector2.UnitX);

        for (int i = 0; i < 25 && game.RoomId == AdventureWorld.GoldCastle; i++)
            game.Update(0.05f, -Vector2.UnitY);

        Assert.True(game.IsGateOpen(AdventureWorld.GoldCastle));
        Assert.Equal(AdventureWorld.GoldInterior, game.RoomId);
        Assert.Equal(game.RoomId, game.Held!.Room);
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
