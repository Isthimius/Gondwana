namespace Gondwana;

/// <summary>
/// Defines a contract for dispatching actions to the engine's background thread in a thread-safe manner.
/// Implementations of this interface provide mechanisms for posting work items from any thread to be
/// executed on the engine's dedicated update loop thread, ensuring thread-safe access to engine state.
/// </summary>
/// <remarks>
/// The dispatcher pattern allows external code to safely interact with engine state by queueing actions
/// that will be executed on the correct thread during the engine's update cycle. This is essential for
/// maintaining thread safety when multiple threads need to modify or access engine resources.
/// </remarks>
public interface IEngineDispatcher
{
    /// <summary>
    /// Gets a value indicating whether the current thread is the engine thread to which
    /// this dispatcher is bound.
    /// </summary>
    /// <value>
    /// <c>true</c> if the current thread is the engine thread; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// This property can be used to optimize action execution by running actions inline when
    /// already on the engine thread, avoiding unnecessary queueing overhead. It also allows
    /// external code to determine if thread-safe access to engine state is required.
    /// </remarks>
    bool IsOnEngineThread { get; }

    /// <summary>
    /// Posts an action to be executed on the engine thread. Implementations may execute the action
    /// inline if already on the engine thread, or queue it for later execution during the engine's
    /// update cycle.
    /// </summary>
    /// <param name="action">
    /// The action to execute on the engine thread. Implementations should handle <c>null</c> gracefully
    /// by ignoring the request.
    /// </param>
    /// <remarks>
    /// This method is thread-safe and can be called from any thread. The exact timing of action execution
    /// depends on the implementation, but queued actions are typically executed during the next call to
    /// <see cref="Drain"/> on the engine thread.
    /// </remarks>
    void Post(Action action);

    /// <summary>
    /// Executes all queued actions that have been posted to this dispatcher since the last call to
    /// <see cref="Drain"/>. This method should be called regularly by the engine's main update loop
    /// to process pending work items.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method should only be called from the engine thread to maintain thread safety guarantees.
    /// It processes actions in the order they were posted (FIFO), executing each one until the queue
    /// is empty.
    /// </para>
    /// <para>
    /// Implementations should handle exceptions thrown by actions appropriately to prevent disruption
    /// of the engine's main loop.
    /// </para>
    /// </remarks>
    void Drain();

    /// <summary>
    /// Binds this dispatcher to the current thread, establishing it as the engine thread.
    /// This method should be called once during engine startup from the thread that will run
    /// the engine's main update loop.
    /// </summary>
    /// <remarks>
    /// After binding, the dispatcher can identify whether subsequent calls are being made from
    /// the engine thread, enabling optimizations such as inline execution of actions posted from
    /// the engine thread itself.
    /// </remarks>
    void BindToCurrentThread();
}
