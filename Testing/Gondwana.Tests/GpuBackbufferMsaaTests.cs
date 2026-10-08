using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using SkiaSharp;

namespace Gondwana.Tests;

/// <summary>
/// Contains regression tests for gpu backbuffer msaa.
/// </summary>
[Collection("Global engine state")]
public sealed class GpuBackbufferMsaaTests : IDisposable
{
    private readonly int _originalMsaaSampleCount;

    /// <summary>
    /// Initializes a new instance of the <c>GpuBackbufferMsaaTests</c> class.
    /// </summary>
    public GpuBackbufferMsaaTests()
    {
        _originalMsaaSampleCount = Engine.Instance.Configuration.MsaaSampleCount;
        Engine.Instance.Configuration.MsaaSampleCount = 1;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Engine.Instance.Configuration.MsaaSampleCount = _originalMsaaSampleCount;
    }

    /// <summary>
    /// Verifies new backbuffer requires initial gpu surface configuration.
    /// </summary>
    [Fact]
    public void NewBackbufferRequiresInitialGpuSurfaceConfiguration()
    {
        using var backbuffer = new GpuBackbuffer(320, 200);

        Assert.Equal(1, backbuffer.MsaaSampleCount);
        Assert.Equal(1, backbuffer.ActualMsaaSampleCount);
        Assert.Equal(1, backbuffer.MaxSupportedMsaaSampleCount);
        Assert.True(backbuffer.IsMsaaSurfaceRecreationPending);

        MarkCurrentConfigurationApplied(backbuffer);

        Assert.False(backbuffer.IsMsaaSurfaceRecreationPending);
    }

    /// <summary>
    /// Verifies changing msaa after surface configuration requests recreation.
    /// </summary>
    [Fact]
    public void ChangingMsaaAfterSurfaceConfigurationRequestsRecreation()
    {
        using var backbuffer = new GpuBackbuffer(320, 200);
        MarkCurrentConfigurationApplied(backbuffer);

        backbuffer.MsaaSampleCount = 4;

        Assert.Equal(4, backbuffer.MsaaSampleCount);
        Assert.Equal(1, backbuffer.ActualMsaaSampleCount);
        Assert.True(backbuffer.IsMsaaSurfaceRecreationPending);

        var configuration = backbuffer.GetMsaaConfigurationSnapshot();
        backbuffer.MarkMsaaConfigurationApplied(configuration.Revision, actualSampleCount: 4);

        Assert.Equal(4, backbuffer.ActualMsaaSampleCount);
        Assert.False(backbuffer.IsMsaaSurfaceRecreationPending);
    }

    /// <summary>
    /// Verifies engine configuration change propagates and requests recreation.
    /// </summary>
    [Fact]
    public void EngineConfigurationChangePropagatesAndRequestsRecreation()
    {
        using var host = new RenderSurfaceHost<GpuBackbuffer>(new TestAdapter(320, 200));
        using var backbuffer = Assert.IsType<GpuBackbuffer>(host.Backbuffer);
        MarkCurrentConfigurationApplied(backbuffer);

        Engine.Instance.Configuration.MsaaSampleCount = 4;

        Assert.Equal(4, backbuffer.MsaaSampleCount);
        Assert.True(backbuffer.IsMsaaSurfaceRecreationPending);
    }

    /// <summary>
    /// Verifies reassigning same msaa does not request another recreation.
    /// </summary>
    [Fact]
    public void ReassigningSameMsaaDoesNotRequestAnotherRecreation()
    {
        using var backbuffer = new GpuBackbuffer(320, 200);
        backbuffer.MsaaSampleCount = 4;
        MarkCurrentConfigurationApplied(backbuffer);

        backbuffer.MsaaSampleCount = 4;

        Assert.False(backbuffer.IsMsaaSurfaceRecreationPending);
    }

    /// <summary>
    /// Verifies unsupported fallback can be recorded without retry loop.
    /// </summary>
    [Fact]
    public void UnsupportedFallbackCanBeRecordedWithoutRetryLoop()
    {
        using var backbuffer = new GpuBackbuffer(320, 200);
        backbuffer.MsaaSampleCount = 8;

        var configuration = backbuffer.GetMsaaConfigurationSnapshot();
        backbuffer.MarkMsaaConfigurationApplied(configuration.Revision, actualSampleCount: 1);

        Assert.Equal(8, backbuffer.MsaaSampleCount);
        Assert.Equal(1, backbuffer.ActualMsaaSampleCount);
        Assert.False(backbuffer.IsMsaaSurfaceRecreationPending);
    }

    /// <summary>
    /// Verifies msaa sample count clamps values below one.
    /// </summary>
    [Fact]
    public void MsaaSampleCountClampsValuesBelowOne()
    {
        using var backbuffer = new GpuBackbuffer(320, 200);
        MarkCurrentConfigurationApplied(backbuffer);

        backbuffer.MsaaSampleCount = 0;

        Assert.Equal(1, backbuffer.MsaaSampleCount);
        Assert.False(backbuffer.IsMsaaSurfaceRecreationPending);
    }

    private static void MarkCurrentConfigurationApplied(GpuBackbuffer backbuffer)
    {
        var configuration = backbuffer.GetMsaaConfigurationSnapshot();
        backbuffer.MarkMsaaConfigurationApplied(configuration.Revision, configuration.SampleCount);
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
            bufferImage.Dispose();
        }
    }
}
