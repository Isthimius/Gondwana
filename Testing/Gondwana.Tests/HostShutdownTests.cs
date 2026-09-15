using System.Reflection;
using Gondwana.Hosting;

namespace Gondwana.Tests;

[Collection("Global engine state")]
public sealed class HostShutdownTests
{
    [Fact]
    public async Task Dispose_WaitsForRenderingBeforeCallingResourceCleanupHooks()
    {
        var cycleField = typeof(Engine).GetField("_cycleTask", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var originalCycle = cycleField.GetValue(Engine.Instance);
        var cycle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new ShutdownHost();
        typeof(GameHostBase).GetField("_engineStarted", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, true);
        typeof(GameHostBase).GetField("_engineInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, true);
        cycleField.SetValue(Engine.Instance, cycle.Task);
        var disposing = Task.Run(() => Assert.Throws<CleanupReachedException>(() => host.Dispose()));
        try
        {
            await host.SchedulingStopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(() => disposing.WaitAsync(TimeSpan.FromMilliseconds(100)));
            Assert.False(host.CleanupReached);
            cycle.SetResult();
            await disposing.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(host.CleanupReached);
        }
        finally
        {
            cycle.TrySetResult();
            await disposing;
            cycleField.SetValue(Engine.Instance, originalCycle);
        }
    }

    private sealed class CleanupReachedException : Exception { }

    private sealed class ShutdownHost : GameHostBase
    {
        public TaskCompletionSource SchedulingStopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CleanupReached { get; private set; }
        protected override void ConfigurePlatform() { }
        protected override void StopEngineCore() => SchedulingStopped.SetResult();
        protected override void OnDisposing()
        {
            CleanupReached = true;
            // End the probe before disposing the process-wide singleton used by other tests.
            throw new CleanupReachedException();
        }
    }
}
