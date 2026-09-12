using System.Drawing;
using Gondwana.Demos.Platformer;

namespace Gondwana.Tests;

public sealed class PlatformerEnemyTests
{
    [Fact]
    public void FastFallAcrossHeadStomps()
    {
        Assert.True(EnemyContact.IsStomp(
            new Rectangle(10, 0, 18, 28), new Rectangle(10, 80, 18, 28),
            new Rectangle(8, 40, 26, 29), new Rectangle(8, 40, 26, 29), 18f));
    }

    [Theory]
    [InlineData(14f)]
    [InlineData(0f)]
    [InlineData(-14f)]
    public void SideContactNeverStomps(float verticalVelocity)
    {
        Assert.False(EnemyContact.IsStomp(
            new Rectangle(0, 40, 18, 28), new Rectangle(10, 40, 18, 28),
            new Rectangle(20, 40, 26, 29), new Rectangle(20, 40, 26, 29), verticalVelocity));
    }

    [Fact]
    public void UpwardVelocityDoesNotStomp()
    {
        Assert.False(EnemyContact.IsStomp(
            new Rectangle(10, 0, 18, 28), new Rectangle(10, 30, 18, 28),
            new Rectangle(8, 40, 26, 29), new Rectangle(8, 40, 26, 29), -14f));
    }

    [Fact]
    public void HorizontalOverlapMustExistAtHeadCrossing()
    {
        Assert.False(EnemyContact.IsStomp(
            new Rectangle(0, 0, 18, 28), new Rectangle(50, 24, 18, 28),
            new Rectangle(50, 40, 26, 29), new Rectangle(50, 40, 26, 29), 14f));
    }

    [Fact]
    public void EnemyArtAlternatesFeetAndEndsFullyTransparent()
    {
        using var bitmap = PlatformerArt.CreateTilesheetBitmap();
        var first = PlatformerArt.EnemyWalkFrame * PlatformerArt.TileSize;
        var second = first + PlatformerArt.TileSize;
        Assert.True(bitmap.GetPixel(first + 8, 30).Alpha > 0);
        Assert.Equal(0, bitmap.GetPixel(second + 8, 30).Alpha);
        Assert.Equal(0, bitmap.GetPixel(first + 24, 30).Alpha);
        Assert.True(bitmap.GetPixel(second + 24, 30).Alpha > 0);

        long previousAlpha = long.MaxValue;
        for (var frame = 0; frame < PlatformerArt.EnemyFadeFrames; frame++)
        {
            long alpha = 0;
            for (var y = 0; y < PlatformerArt.TileSize; y++)
            for (var x = 0; x < PlatformerArt.TileSize; x++)
            {
                var pixel = bitmap.GetPixel(
                    (PlatformerArt.EnemyFlattenedFrame + frame) * PlatformerArt.TileSize + x, y);
                if (y < 24)
                    Assert.Equal(0, pixel.Alpha);
                alpha += pixel.Alpha;
            }
            Assert.True(alpha < previousAlpha);
            previousAlpha = alpha;
        }
        Assert.Equal(0L, previousAlpha);
    }
}
