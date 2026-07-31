using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Tests;

[CollectionDefinition("GPU refresh queue", DisableParallelization = true)]
public sealed class GpuRefreshQueueCollection
{
    public const string Name = "GPU refresh queue";
}

/// <summary>
/// Verifies that full-frame GL rendering neither retains nor accepts dirty regions,
/// while bitmap rendering continues to use RefreshQueue normally.
/// </summary>
[Collection(GpuRefreshQueueCollection.Name)]
public sealed class GpuRefreshQueueTests
{
    public GpuRefreshQueueTests()
    {
        Engine.Instance.EngineDispatcher.BindToCurrentThread();
        Engine.Instance.EngineDispatcher.Drain();
    }

    [Fact]
    public void BindGpuHost_ClearsPreBindQueueAndRejectsFutureWorldRects()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        var initialRect = new Rectangle(0, 0, 16, 16);
        layer.RefreshQueue.AddWorldRect(initialRect);
        Assert.True(layer.RefreshQueue.IsDirty);

        using var host = CreateGpuHost();
        host.Bind(scene);

        Assert.False(scene.UsesDirtyRegionRendering);
        Assert.False(layer.RefreshQueue.IsDirty);

        layer.RefreshQueue.AddWorldRect(new Rectangle(16, 0, 16, 16));

        Assert.False(layer.RefreshQueue.IsDirty);
    }

    [Fact]
    public void DisabledQueue_RejectsViewRectBeforeDoingViewConversion()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        using var host = CreateGpuHost();
        host.Bind(scene);

        var exception = Record.Exception(
            () => layer.RefreshQueue.AddViewScreenRect(
                null!,
                null!,
                new Rectangle(0, 0, 16, 16)));

        Assert.Null(exception);
        Assert.False(layer.RefreshQueue.IsDirty);
    }

    [Fact]
    public void BindBitmapHost_ContinuesAcceptingDirtyRegions()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        using var host = CreateBitmapHost();
        host.Bind(scene);

        layer.RefreshQueue.AddWorldRect(new Rectangle(0, 0, 16, 16));

        Assert.True(scene.UsesDirtyRegionRendering);
        Assert.True(layer.RefreshQueue.IsDirty);
    }

    [Fact]
    public void RebindFromGpuToBitmap_ReenablesDirtyRegionTracking()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);

        using (var gpuHost = CreateGpuHost())
        {
            gpuHost.Bind(scene);
            layer.RefreshQueue.AddWorldRect(new Rectangle(0, 0, 16, 16));
            Assert.False(layer.RefreshQueue.IsDirty);
        }

        using var bitmapHost = CreateBitmapHost();
        bitmapHost.Bind(scene);
        layer.RefreshQueue.AddWorldRect(new Rectangle(16, 0, 16, 16));

        Assert.True(layer.RefreshQueue.IsDirty);
    }

    private static RenderSurfaceHost<GpuBackbuffer> CreateGpuHost() =>
        new(new TestAdapter(320, 200));

    private static RenderSurfaceHost<BitmapBackbuffer> CreateBitmapHost() =>
        new(new TestAdapter(320, 200));

    private sealed class TestAdapter(int width, int height)
        : RenderSurfaceAdapterBase(width, height)
    {
        public override void Present(
            SKImage bufferImage,
            SKRectI bufferRect,
            SKRect destRect)
        {
        }
    }
}
