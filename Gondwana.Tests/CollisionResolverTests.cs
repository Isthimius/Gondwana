using System.Drawing;
using Gondwana.Collisions;

namespace Gondwana.Tests;

public sealed class CollisionResolverTests
{
    [Theory]
    [InlineData(4, 10, 0f, 0f, 20f, 2f, true)]
    [InlineData(4, 10, 0f, 0f, 2f, 20f, false)]
    [InlineData(10, 4, 0f, 0f, 2f, 20f, false)]
    [InlineData(10, 4, 0f, 0f, 20f, 2f, true)]
    public void ShouldResolveAlongXAxis_UsesCenterDeltasToCorrectAmbiguousProjectionCases(
        int overlapWidth,
        int overlapHeight,
        float centerX,
        float centerY,
        float otherCenterX,
        float otherCenterY,
        bool expectedResolveAlongX)
    {
        var overlap = new Rectangle(0, 0, overlapWidth, overlapHeight);

        bool resolveAlongX = CollisionResolver.ShouldResolveAlongXAxis(
            overlap,
            centerX,
            centerY,
            otherCenterX,
            otherCenterY);

        Assert.Equal(expectedResolveAlongX, resolveAlongX);
    }
}
