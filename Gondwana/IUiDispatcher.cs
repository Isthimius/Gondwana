namespace Gondwana;

/// <summary>
/// Defines a contract for dispatching actions to the UI thread in a thread-safe manner.
/// Implementations of this interface provide mechanisms for posting or sending work items from
/// any thread to be executed on the application's main UI thread, ensuring thread-safe access
/// to UI components and controls.
/// </summary>
/// <remarks>
/// <para>
/// The UI dispatcher pattern is essential for cross-thread communication in applications with
/// a dedicated UI thread. It allows background threads (such as the engine's update loop) to
/// safely schedule UI updates without causing threading violations.
/// </para>
/// <para>
/// Most operations should use <see cref="Post"/> (asynchronous) rather than <see cref="Send"/>
/// (synchronous) to avoid potential deadlocks and improve responsiveness.
/// </para>
/// </remarks>
public interface IUiDispatcher
{
    /// <summary>
    /// Gets a value indicating whether the current thread is the UI thread to which
    /// this dispatcher is bound.
    /// </summary>
    /// <value>
    /// <c>true</c> if the current thread is the UI thread; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// This property can be used to optimize action execution by running UI updates inline when
    /// already on the UI thread, avoiding unnecessary marshalling overhead. It also allows code
    /// to determine if cross-thread marshalling is required before accessing UI components.
    /// </remarks>
    bool IsOnUIThread { get; }

    /// <summary>
    /// Asynchronously posts an action to be executed on the UI thread. This method queues
    /// the action and returns immediately without waiting for execution to complete.
    /// </summary>
    /// <param name="action">
    /// The action to execute on the UI thread. The action will be queued and executed
    /// asynchronously on the UI thread's message loop.
    /// </param>
    /// <remarks>
    /// <para>
    /// This is the preferred method for dispatching UI operations from background threads.
    /// It is non-blocking and avoids potential deadlocks that can occur with synchronous dispatch.
    /// </para>
    /// <para>
    /// The action is queued to the UI thread's message pump and will be executed when the UI
    /// thread processes it. There is no guarantee about the exact timing of execution, and the
    /// calling thread continues immediately without waiting.
    /// </para>
    /// <para>
    /// This method is thread-safe and can be called from any thread.
    /// </para>
    /// </remarks>
    void Post(Action action);        // async, preferred

    /// <summary>
    /// Synchronously sends an action to be executed on the UI thread, blocking the calling thread
    /// until the action completes. Use with caution to avoid deadlocks.
    /// </summary>
    /// <param name="action">
    /// The action to execute on the UI thread. The calling thread will block until this action
    /// completes execution on the UI thread.
    /// </param>
    /// <remarks>
    /// <para>
    /// This method provides synchronous dispatch to the UI thread, blocking the caller until
    /// the action has been executed and completed. This can be useful when the calling code
    /// needs to ensure UI updates have finished before proceeding, or when a return value
    /// from the UI operation is required.
    /// </para>
    /// <para>
    /// <b>Warning:</b> Using <see cref="Send"/> can lead to deadlocks if the UI thread is waiting
    /// for the calling thread, or if there are circular dependencies. It should be avoided on
    /// web platforms and in single-threaded environments where blocking the caller would also
    /// block the UI thread.
    /// </para>
    /// <para>
    /// In most cases, prefer using <see cref="Post"/> for asynchronous, non-blocking dispatch.
    /// </para>
    /// <para>
    /// This method is thread-safe and can be called from any thread, but exercise caution to
    /// avoid deadlock scenarios.
    /// </para>
    /// </remarks>
    void Send(Action action);        // sync; avoid on web platforms
}