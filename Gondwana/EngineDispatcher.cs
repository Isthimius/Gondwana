using System.Collections.Concurrent;

namespace Gondwana;

/// <summary>
/// Provides thread-safe dispatching of actions to the engine's background thread.
/// This dispatcher allows external code to safely post work items that should execute
/// on the engine's dedicated update loop thread, ensuring thread-safe access to engine state.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="EngineDispatcher"/> uses a concurrent queue to collect actions posted
/// from any thread and drains them on the engine thread during each engine cycle.
/// Actions posted from the engine thread itself can optionally be executed inline
/// for improved performance.
/// </para>
/// <para>
/// This dispatcher is bound to a specific thread using <see cref="BindToCurrentThread"/>
/// during engine startup and is drained by the engine's main loop via <see cref="Drain"/>.
/// </para>
/// </remarks>
public sealed class EngineDispatcher : IEngineDispatcher
{
    private readonly ConcurrentQueue<Action> _queue = new();
    private int _engineThreadId;

    /// <summary>
    /// Binds this dispatcher to the current thread, establishing it as the engine thread.
    /// This method should be called once during engine startup from the thread that will
    /// run the engine's main update loop.
    /// </summary>
    /// <remarks>
    /// After binding, the dispatcher uses the current thread's managed thread ID to determine
    /// whether subsequent calls to <see cref="Post"/> are being made from the engine thread,
    /// allowing for optimizations such as inline execution. This method is typically called
    /// by the engine's main loop initialization code.
    /// </remarks>
    public void BindToCurrentThread()
        => _engineThreadId = Environment.CurrentManagedThreadId;

    /// <summary>
    /// Gets a value indicating whether the current thread is the engine thread to which
    /// this dispatcher is bound.
    /// </summary>
    /// <value>
    /// <c>true</c> if the current thread's managed thread ID matches the engine thread ID
    /// set by <see cref="BindToCurrentThread"/>; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// This property is used internally to optimize action execution by running actions
    /// inline when already on the engine thread, avoiding unnecessary queueing overhead.
    /// It can also be used by external code to determine if thread-safe access to engine
    /// state is required.
    /// </remarks>
    public bool IsOnEngineThread
        => Environment.CurrentManagedThreadId == _engineThreadId;

    /// <summary>
    /// Posts an action to be executed on the engine thread. If the current thread is already
    /// the engine thread, the action is executed inline immediately; otherwise, it is queued
    /// for execution during the next call to <see cref="Drain"/>.
    /// </summary>
    /// <param name="action">
    /// The action to execute on the engine thread. If <c>null</c>, this method returns
    /// immediately without taking any action.
    /// </param>
    /// <remarks>
    /// <para>
    /// This method is thread-safe and can be called from any thread. Actions are executed
    /// in the order they are posted when multiple threads are posting concurrently, though
    /// the exact ordering may vary due to thread scheduling.
    /// </para>
    /// <para>
    /// The inline execution optimization for engine thread calls eliminates queueing overhead
    /// and guarantees immediate execution, which is useful for performance-critical operations
    /// that are already running on the correct thread.
    /// </para>
    /// </remarks>
    public void Post(Action action)
    {
        if (action is null) return;

        // Optional: run inline if already on engine thread.
        if (IsOnEngineThread) { action(); return; }

        _queue.Enqueue(action);
    }

    /// <summary>
    /// Executes all queued actions that have been posted to this dispatcher since the last
    /// call to <see cref="Drain"/>. This method should be called regularly by the engine's
    /// main update loop to process pending work items.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method dequeues and executes each action in FIFO order until the queue is empty.
    /// It should only be called from the engine thread to maintain thread safety guarantees.
    /// </para>
    /// <para>
    /// If an action throws an exception, it will propagate to the caller and may interrupt
    /// processing of remaining queued actions. The engine's main loop should handle such
    /// exceptions appropriately to maintain stability.
    /// </para>
    /// <para>
    /// Actions posted during the execution of <see cref="Drain"/> (including those posted
    /// by actions being drained) will not be executed until the next call to <see cref="Drain"/>.
    /// </para>
    /// </remarks>
    public void Drain()
    {
        while (_queue.TryDequeue(out var a))
            a();
    }
}
