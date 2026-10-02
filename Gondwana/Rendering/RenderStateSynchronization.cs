namespace Gondwana.Rendering;

/// <summary>
/// Serializes live render-state mutation on the engine thread with GPU scene traversal
/// on platform GL threads.
/// </summary>
/// <remarks>
/// Holding the shared gate across an engine cycle and each live GPU render prevents native
/// Skia resources from being configured or disposed while the GL thread is using them.
///
/// GPU paint callbacks receive admission priority once they are waiting for the gate. Without
/// that handoff, the unconstrained engine loop can repeatedly release and immediately reacquire
/// the monitor thousands of times per second, starving a waiting GL thread even when each engine
/// cycle is individually short.
/// </remarks>
internal static class RenderStateSynchronization
{
    private static readonly ManualResetEventSlim NoPendingGpuRenderers = new(initialState: true);
    private static int _pendingGpuRenderers;

    internal static object SyncRoot { get; } = new();

    internal static bool HasPendingGpuRenderers =>
        Volatile.Read(ref _pendingGpuRenderers) > 0;

    /// <summary>
    /// Called by the engine loop before entering the shared render-state monitor.
    /// If a GPU renderer is already waiting, yield admission until that renderer has acquired
    /// the monitor. The renderer still serializes normally with the engine once admitted.
    /// </summary>
    internal static void WaitForPendingGpuRenderers()
    {
        while (Volatile.Read(ref _pendingGpuRenderers) > 0)
            NoPendingGpuRenderers.Wait();
    }

    /// <summary>
    /// Registers a GPU renderer as waiting and enters the shared render-state monitor.
    /// </summary>
    internal static void EnterGpuRender()
    {
        if (Interlocked.Increment(ref _pendingGpuRenderers) == 1)
            NoPendingGpuRenderers.Reset();

        Monitor.Enter(SyncRoot);

        if (Interlocked.Decrement(ref _pendingGpuRenderers) == 0)
            NoPendingGpuRenderers.Set();
    }

    /// <summary>
    /// Leaves the shared render-state monitor after a GPU render.
    /// </summary>
    internal static void ExitGpuRender() => Monitor.Exit(SyncRoot);
}
