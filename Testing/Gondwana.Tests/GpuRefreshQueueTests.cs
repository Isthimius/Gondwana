using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Tests;

/// <summary>
/// Represents gpu refresh queue collection.
/// </summary>
[CollectionDefinition("GPU refresh queue", DisableParallelization = true)]
public sealed class GpuRefreshQueueCollection
{
    /// <summary>
    /// The name.
    /// </summary>
    public const string Name = "GPU refresh queue";
}

/// <summary>
/// Verifies that full-frame GL rendering neither retains nor accepts dirty regions,
/// while bitmap rendering continues to use RefreshQueue normally.
/// </summary>
[Collection(GpuRefreshQueueCollection.Name)]
public sealed class GpuRefreshQueueTests
{
    /// <summary>
    /// Initializes a new instance of the <c>GpuRefreshQueueTests</c> class.
    /// </summary>
    public GpuRefreshQueueTests()
    {
        Engine.Instance.EngineDispatcher.BindToCurrentThread();
        Engine.Instance.EngineDispatcher.Drain();
    }

    /// <summary>
    /// Verifies bind gpu host clears pre bind queue and rejects future world rects.
    /// </summary>
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

    /// <summary>
    /// Verifies disabled queue rejects view rect before doing view conversion.
    /// </summary>
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

    /// <summary>
    /// Verifies bind bitmap host continues accepting dirty regions.
    /// </summary>
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

    /// <summary>
    /// Verifies rebind from gpu to bitmap reenables dirty region tracking.
    /// </summary>
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

    /// <summary>
    /// Verifies current frame on gpu host skips dirty bounds calculation.
    /// </summary>
    [Fact]
    public void CurrentFrame_OnGpuHost_SkipsDirtyBoundsCalculation()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        using var host = CreateGpuHost();
        host.Bind(scene);
        using var tile = new CountingTile(layer);

        tile.CurrentFrame = default;

        Assert.Equal(0, tile.DrawLocationWorldReadCount);
    }

    /// <summary>
    /// Verifies current frame on bitmap host preserves dirty bounds calculation.
    /// </summary>
    [Fact]
    public void CurrentFrame_OnBitmapHost_PreservesDirtyBoundsCalculation()
    {
        using var scene = new Scene();
        var layer = scene.AddLayer(4, 4);
        using var host = CreateBitmapHost();
        host.Bind(scene);
        using var tile = new CountingTile(layer);

        tile.CurrentFrame = default;

        Assert.Equal(2, tile.DrawLocationWorldReadCount);
    }

    private static RenderSurfaceHost<GpuBackbuffer> CreateGpuHost() =>
        new(new TestAdapter(320, 200));

    private static RenderSurfaceHost<BitmapBackbuffer> CreateBitmapHost() =>
        new(new TestAdapter(320, 200));

    private sealed class CountingTile(SceneLayer sceneLayer) : Tile
    {
        /// <summary>
        /// Gets the draw location world read count.
        /// </summary>
        public int DrawLocationWorldReadCount { get; private set; }

        /// <inheritdoc/>
        public override bool IsPositionFixed => true;

        /// <inheritdoc/>
        public override Rectangle DrawLocationWorld
        {
            get
            {
                DrawLocationWorldReadCount++;
                return new Rectangle(0, 0, 16, 16);
            }
        }

        /// <inheritdoc/>
        public override PointF SceneLayerCoordinates => PointF.Empty;

        /// <inheritdoc/>
        public override SceneLayer SceneLayer { get; } = sceneLayer;
    }

    private sealed class TestAdapter(int width, int height)
        : RenderSurfaceAdapterBase(width, height)
    {
        /// <inheritdoc/>
        public override void Present(
            SKImage bufferImage,
            SKRectI bufferRect,
            SKRect destRect)
        {
        }
    }
}
