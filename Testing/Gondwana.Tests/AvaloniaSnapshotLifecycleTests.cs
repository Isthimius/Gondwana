using Gondwana.Avalonia.Rendering;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;

namespace Gondwana.Tests;

/// <summary>
/// Contains tests for avalonia snapshot lifecycle.
/// </summary>
[Collection("Effects rendering")]
public sealed class AvaloniaSnapshotLifecycleTests
{
    /// <summary>
    /// Verifies that context teardown preserves host and producer until permanent disposal.
    /// </summary>
    [Fact]
    public void ContextTeardownPreservesHostAndProducer_UntilPermanentDisposal()
    {
        Engine.Instance.EngineDispatcher.BindToCurrentThread();
        using var control = new Control();
        using var scene = new Scene();
        control.Host.Bind(scene, false);
        var host = control.Host;
        host.ProduceRenderFrameSnapshot(1);
        control.Deinitialize();
        Assert.Same(scene, host.Scene);
        Assert.Contains(host, RenderSurfaceHostRegistry.All);

        // Recording requires no GL surface, even while the context is unavailable.
        host.ProduceRenderFrameSnapshot(2);
        Assert.Equal(2, host.FrameMailbox.Counters.Published);
        Assert.Equal(1, host.FrameMailbox.Counters.Dropped);
        control.Dispose();
        Assert.DoesNotContain(host, RenderSurfaceHostRegistry.All);
        Assert.Equal(0, host.FrameMailbox.Counters.InUse);
        control.Deinitialize(); // permanent shutdown and deinit are order-independent
    }

    /// <summary>
    /// Verifies that release context leaves no old surface but preserves logical configuration.
    /// </summary>
    [Fact]
    public void ReleaseContextLeavesNoOldSurface_ButPreservesLogicalConfiguration()
    {
        using var buffer = new GpuBackbuffer(32, 16) { MsaaSampleCount = 4 };
        buffer.ReleaseContext();
        Assert.Throws<InvalidOperationException>(() => buffer.Canvas);
        Assert.Equal(32, buffer.Width);
        Assert.Equal(16, buffer.Height);
        Assert.Equal(4, buffer.MsaaSampleCount);
        buffer.ReleaseContext();
    }

    private sealed class Control : AvaloniaGpuRenderSurfaceControl
    {
        /// <summary>
        /// Verifies that deinitialize.
        /// </summary>
        internal void Deinitialize() => OnOpenGlDeinit(null!);
    }
}
