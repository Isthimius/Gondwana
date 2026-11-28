using System.Numerics;
using Gondwana.Demos.Asteroids;

namespace Asteroids.Tests;

public sealed class AsteroidsGameTests
{
    [Fact]
    public void HudFontAndLicenseAreEmbeddedInSharedAssembly()
    {
        var assembly = typeof(AsteroidsGame).Assembly;
        using var font = assembly.GetManifestResourceStream("Gondwana.Demos.Asteroids.Fonts.ArchitectsDaughter-Regular.ttf");
        using var license = assembly.GetManifestResourceStream("Gondwana.Demos.Asteroids.Fonts.OFL.txt");
        Assert.NotNull(font);
        Assert.NotNull(license);
        Assert.True(font.Length > 0);
        Assert.True(license.Length > 0);
    }

    [Theory]
    [InlineData(640f, 360f, 15.5f, 8.5f)]
    [InlineData(0f, 0f, -0.5f, -0.5f)]
    [InlineData(1280f, 720f, 31.5f, 17.5f)]
    [InlineData(-40f, 760f, -1.5f, 18.5f)]
    public void CenterAlignedSpriteGridPositionPreservesSimulationCenter(
        float worldX, float worldY, float gridX, float gridY)
    {
        var center = new Vector2(worldX, worldY);
        Vector2 grid = AsteroidsRuntime.ToCenteredSpriteGridPosition(center);
        Assert.Equal(new Vector2(gridX, gridY), grid);

        Vector2 visualCenter = (grid + new Vector2(0.5f, 0.5f)) * AsteroidsGame.TileSize;
        Assert.Equal(center, visualCenter);
    }

    [Fact]
    public void WrapHandlesBothAxesAndNegativePositions()
    {
        Assert.Equal(new Vector2(AsteroidsGame.Width - 5, 7), AsteroidsGame.Wrap(new Vector2(-5, AsteroidsGame.Height + 7)));
        Assert.Equal(4f, AsteroidsGame.DistanceSquared(new Vector2(1, 0), new Vector2(AsteroidsGame.Width - 1, 0)));
    }

    [Fact]
    public void StartAndResetEstablishExpectedState()
    {
        var game = new AsteroidsGame(42);
        Assert.Equal(AsteroidsGame.Phase.Ready, game.State);
        game.Start();
        Assert.Equal(AsteroidsGame.Phase.Playing, game.State);
        Assert.Equal(1, game.Wave);
        Assert.Equal(4, game.Bodies.Count(b => b.Type == AsteroidsGame.Kind.Rock));
        Assert.Equal(3, game.Lives);
        game.Reset();
        Assert.Equal(AsteroidsGame.Phase.Ready, game.State);
        Assert.Empty(game.Bodies);
        Assert.Equal(0, game.Score);
    }

    [Fact]
    public void PauseStopsSimulationAndThrustProducesInertia()
    {
        var game = new AsteroidsGame(42);
        game.Start();
        game.Update(0.05f, 0f, true, false);
        Assert.True(game.Ship.Velocity.Length() > 0f);
        Vector2 position = game.Ship.Position;
        game.TogglePause();
        game.Update(0.05f, 0f, true, true);
        Assert.Equal(position, game.Ship.Position);
        game.TogglePause();
        game.Update(0.05f, 0f, false, false);
        Assert.NotEqual(position, game.Ship.Position);
    }

    [Fact]
    public void ShotSplitsLargeRockAndAwardsPoints()
    {
        var game = new AsteroidsGame(42);
        game.Start();
        var sounds = new List<AsteroidsGame.Sound>();
        game.SoundRequested += sounds.Add;
        var rock = game.Bodies.First(b => b.Type == AsteroidsGame.Kind.Rock);
        rock.Position = game.Ship.Position + new Vector2(0, -45);
        rock.Velocity = Vector2.Zero;
        game.Update(0.01f, 0f, false, true);
        Assert.Equal(20, game.Score);
        Assert.Equal(new[] { AsteroidsGame.Sound.PlayerShot, AsteroidsGame.Sound.LargeExplosion }, sounds);
        Assert.Equal(2, game.Bodies.Count(b => b.Type == AsteroidsGame.Kind.Rock && b.Size == 2));
    }

    [Fact]
    public void PlayerFireSoundMatchesActualShotsAndStopsWhilePaused()
    {
        var game = new AsteroidsGame(42);
        var sounds = new List<AsteroidsGame.Sound>();
        game.SoundRequested += sounds.Add;
        game.Start();
        game.Update(0.05f, 0f, false, true);
        game.Update(0.05f, 0f, false, true);
        game.TogglePause();
        game.Update(0.05f, 0f, false, true);

        Assert.Single(sounds, sound => sound == AsteroidsGame.Sound.PlayerShot);
    }

    [Fact]
    public void SynthesizedEffectsAreDistinctValidPcmWaves()
    {
        var unique = new HashSet<string>();

        foreach (var sound in Enum.GetValues<AsteroidsGame.Sound>())
        {
            using var stream = AsteroidsAudio.CreateWave(sound);
            using var reader = new BinaryReader(stream);
            Assert.Equal("RIFF", new string(reader.ReadChars(4)));
            Assert.Equal(stream.Length - 8, (long)reader.ReadInt32());
            Assert.Equal("WAVE", new string(reader.ReadChars(4)));
            Assert.Equal("fmt ", new string(reader.ReadChars(4)));
            Assert.Equal(16, reader.ReadInt32());
            Assert.Equal((short)1, reader.ReadInt16());
            Assert.Equal((short)1, reader.ReadInt16());
            Assert.Equal(22050, reader.ReadInt32());
            stream.Position = 34;
            Assert.Equal((short)16, reader.ReadInt16());
            Assert.Equal("data", new string(reader.ReadChars(4)));
            int length = reader.ReadInt32();
            Assert.Equal(stream.Length - 44, (long)length);
            byte[] samples = reader.ReadBytes(length);
            Assert.Contains(samples, sample => sample != 0);
            Assert.True(unique.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(samples))));
        }
    }
}
