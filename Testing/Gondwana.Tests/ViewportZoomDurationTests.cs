using Gondwana.Rendering.Views;

namespace Gondwana.Tests.Rendering.Views;

/// <summary>
/// Contains regression tests for viewport zoom duration.
/// </summary>
public sealed class ViewportZoomDurationTests
{
    /// <summary>
    /// Verifies zoom to over duration snaps to target when duration elapses.
    /// </summary>
    [Fact]
    public void ZoomToOverDuration_SnapsToTargetWhenDurationElapses()
    {
        var viewport = new Viewport { Zoom = 1f };

        viewport.ZoomToOverDuration(
            targetZoom: 2f,
            durationSeconds: 0.75f);

        viewport.UpdateZoom(0.50f);

        Assert.True(viewport.IsZoomAnimating);
        Assert.NotEqual(2f, viewport.Zoom);

        viewport.UpdateZoom(0.25f);

        Assert.Equal(2f, viewport.Zoom);
        Assert.False(viewport.IsZoomAnimating);
    }

    /// <summary>
    /// Verifies zoom to over duration when frame exceeds remaining time snaps to target.
    /// </summary>
    [Fact]
    public void ZoomToOverDuration_WhenFrameExceedsRemainingTime_SnapsToTarget()
    {
        var viewport = new Viewport { Zoom = 1f };

        viewport.ZoomToOverDuration(
            targetZoom: 0.5f,
            durationSeconds: 0.20f);

        viewport.UpdateZoom(0.25f);

        Assert.Equal(0.5f, viewport.Zoom);
        Assert.False(viewport.IsZoomAnimating);
    }
}
