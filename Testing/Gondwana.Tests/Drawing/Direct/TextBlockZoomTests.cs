using Gondwana.Drawing.Direct;

namespace Gondwana.Tests.Drawing.Direct;

/// <summary>
/// Verifies that TextBlock follows Gondwana's conventional zoom contract:
/// values above one zoom world-space content in, while view-space text
/// remains expressed directly in screen pixels.
/// </summary>
public sealed class TextBlockZoomTests
{
    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(4f)]
    public void ResolveTextScale_SceneLayerMode_UsesViewportZoom(
        float viewportZoom)
    {
        float scale = TextBlock.ResolveTextScale(
            DirectDrawingMode.SceneLayer,
            viewportZoom);

        Assert.Equal(viewportZoom, scale);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(4f)]
    public void ResolveTextScale_ViewMode_RemainsScreenSized(
        float viewportZoom)
    {
        float scale = TextBlock.ResolveTextScale(
            DirectDrawingMode.View,
            viewportZoom);

        Assert.Equal(1f, scale);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void ResolveTextScale_InvalidSceneLayerZoom_FallsBackToOne(
        float viewportZoom)
    {
        float scale = TextBlock.ResolveTextScale(
            DirectDrawingMode.SceneLayer,
            viewportZoom);

        Assert.Equal(1f, scale);
    }
}
