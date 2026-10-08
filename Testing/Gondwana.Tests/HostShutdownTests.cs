using System.Reflection;
using Gondwana.Hosting;

namespace Gondwana.Tests;

/// <summary>
/// Contains regression tests for host shutdown.
/// </summary>
[Collection("Global engine state")]
public sealed class HostShutdownTests
{
    /// <summary>
    /// Verifies dispose waits for rendering before calling resource cleanup hooks.
    /// </summary>
    /// <returns>A task that represents completion of the operation.</returns>
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
        /// <summary>
        /// Gets the scheduling stopped.
        /// </summary>
        public TaskCompletionSource SchedulingStopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>
        /// Gets whether cleanup reached is enabled.
        /// </summary>
        public bool CleanupReached { get; private set; }
        /// <inheritdoc/>
        protected override void ConfigurePlatform() { }
        /// <inheritdoc/>
        protected override void StopEngineCore() => SchedulingStopped.SetResult();
        /// <inheritdoc/>
        protected override void OnDisposing()
        {
            CleanupReached = true;
            // End the probe before disposing the process-wide singleton used by other tests.
            throw new CleanupReachedException();
        }
    }
}
