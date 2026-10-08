using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;

namespace Gondwana.Tests.Drawing.Direct;

/// <summary>
/// Contains regression tests for render state synchronization.
/// </summary>
public sealed class RenderStateSynchronizationTests
{
    /// <summary>
    /// Verifies direct drawing dispose waits for active render state reader.
    /// </summary>
    [Fact]
    public void DirectDrawingDispose_WaitsForActiveRenderStateReader()
    {
        using var host = new TestRenderSurfaceHost();
        host.ViewManager.AddView(new Rectangle(0, 0, 320, 240));
        View view = host.ViewManager.Views[0];

        var rectangle = new DirectRectangle(
            Color.White,
            host,
            view,
            new Rectangle(0, 0, 32, 32));

        using var gateEntered = new ManualResetEventSlim();
        using var releaseGate = new ManualResetEventSlim();
        using var disposeStarted = new ManualResetEventSlim();

        Task holder = Task.Run(() =>
        {
            lock (RenderStateSynchronization.SyncRoot)
            {
                gateEntered.Set();
                releaseGate.Wait();
            }
        });

        Assert.True(gateEntered.Wait(TimeSpan.FromSeconds(5)));

        Task disposer = Task.Run(() =>
        {
            disposeStarted.Set();
            rectangle.Dispose();
        });

        Assert.True(disposeStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.False(disposer.Wait(TimeSpan.FromMilliseconds(100)));

        releaseGate.Set();

        Assert.True(disposer.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(holder.Wait(TimeSpan.FromSeconds(5)));
    }
    /// <summary>
    /// Verifies waiting gpu renderer gets admission before next engine style acquisition.
    /// </summary>
    [Fact]
    public void WaitingGpuRenderer_GetsAdmissionBeforeNextEngineStyleAcquisition()
    {
        using var initialHolderEntered = new ManualResetEventSlim();
        using var releaseInitialHolder = new ManualResetEventSlim();
        using var gpuAcquired = new ManualResetEventSlim();
        using var releaseGpu = new ManualResetEventSlim();
        using var engineAcquired = new ManualResetEventSlim();

        Task initialHolder = Task.Run(() =>
        {
            lock (RenderStateSynchronization.SyncRoot)
            {
                initialHolderEntered.Set();
                releaseInitialHolder.Wait();
            }
        });

        Assert.True(initialHolderEntered.Wait(TimeSpan.FromSeconds(5)));

        Task gpu = Task.Run(() =>
        {
            RenderStateSynchronization.EnterGpuRender();
            try
            {
                gpuAcquired.Set();
                releaseGpu.Wait();
            }
            finally
            {
                RenderStateSynchronization.ExitGpuRender();
            }
        });

        Assert.True(
            SpinWait.SpinUntil(
                () => RenderStateSynchronization.HasPendingGpuRenderers,
                TimeSpan.FromSeconds(5)));

        Task engine = Task.Run(() =>
        {
            RenderStateSynchronization.WaitForPendingGpuRenderers();
            lock (RenderStateSynchronization.SyncRoot)
                engineAcquired.Set();
        });

        releaseInitialHolder.Set();

        Assert.True(gpuAcquired.Wait(TimeSpan.FromSeconds(5)));
        Assert.False(engineAcquired.IsSet);

        releaseGpu.Set();

        Assert.True(engineAcquired.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(Task.WaitAll([initialHolder, gpu, engine], TimeSpan.FromSeconds(5)));
    }

}
