using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;

namespace Gondwana.Tests.Drawing.Direct;

public sealed class RenderStateSynchronizationTests
{
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
}
