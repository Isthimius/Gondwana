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
    // Orthogonal wall-slide: thin X overlap (1px wide, 10px tall) is 10:1 in favor of X.
    // Center-delta must be >10:1 before it can override — a modest 12:9 ratio must not.
    [InlineData(1, 10, 0f, 0f, 9f, 12f, true)]
    // Same wall, mover has slid far: center-delta Y (55) vs X (9) = 6.1:1, still < 10:1 overlap ratio.
    [InlineData(1, 10, 0f, 50f, 9f, 5f, true)]
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

    [Theory]
    [InlineData(true, false, 8, 0, true, false)]
    [InlineData(false, true, 0, 8, false, true)]
    [InlineData(true, true, 10, 2, true, false)]
    [InlineData(true, true, 2, 10, false, true)]
    [InlineData(true, true, 6, 6, true, true)]
    public void SelectVelocityCancellationAxes_PrefersDominantPenetrationAxis_WhenBothAxesCollide(
        bool hitX,
        bool hitY,
        int totalAbsDx,
        int totalAbsDy,
        bool expectedCancelX,
        bool expectedCancelY)
    {
        var (cancelX, cancelY) = CollisionResolver.SelectVelocityCancellationAxes(hitX, hitY, totalAbsDx, totalAbsDy);

        Assert.Equal(expectedCancelX, cancelX);
        Assert.Equal(expectedCancelY, cancelY);
    }
}
