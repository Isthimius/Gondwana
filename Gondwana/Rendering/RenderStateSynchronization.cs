namespace Gondwana.Rendering;

/// <summary>
/// Serializes Engine state and resource disposal with recording and legacy live rendering.
/// Desktop snapshot replay never enters this gate. The GPU admission helpers remain
/// for the synchronous live WebGL/custom-backbuffer path; resize notifications also
/// use SyncRoot briefly to update viewports. Do not use this gate around picture replay.
/// </summary>
internal static class RenderStateSynchronization
{
    private static readonly ManualResetEventSlim NoPendingGpuRenderers = new(initialState: true);
    private static int _pendingGpuRenderers;

    /// <summary>
    /// Gets the monitor used to serialize mutable engine render state with synchronous live rendering.
    /// </summary>
    internal static object SyncRoot { get; } = new();

    /// <summary>
    /// Gets a value indicating whether a synchronous GPU renderer is waiting to enter the shared render-state monitor.
    /// </summary>
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
