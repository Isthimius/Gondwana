using System.Numerics;
using Gondwana.Demos.Asteroids;

namespace Asteroids.Tests;

public sealed class AsteroidsGameTests
{
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
        var rock = game.Bodies.First(b => b.Type == AsteroidsGame.Kind.Rock);
        rock.Position = game.Ship.Position + new Vector2(0, -45);
        rock.Velocity = Vector2.Zero;
        game.Update(0.01f, 0f, false, true);
        Assert.Equal(20, game.Score);
        Assert.Equal(2, game.Bodies.Count(b => b.Type == AsteroidsGame.Kind.Rock && b.Size == 2));
    }
}
