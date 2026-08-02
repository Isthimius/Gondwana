using Gondwana.Rendering.Views;

namespace Gondwana.Tests.Rendering.Views;

public sealed class ViewportZoomDurationTests
{
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
