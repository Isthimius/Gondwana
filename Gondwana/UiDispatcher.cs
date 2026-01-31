namespace Gondwana;

/// <summary>
/// Provides a concrete implementation of <see cref="IUiDispatcher"/> for dispatching actions
/// to the UI thread using a <see cref="SynchronizationContext"/>. This class enables thread-safe
/// marshalling of operations from background threads to the application's main UI thread.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="UiDispatcher"/> wraps a <see cref="SynchronizationContext"/> and records
/// the thread ID at construction time to enable fast thread checking. It provides both
/// asynchronous (<see cref="Post"/>) and synchronous (<see cref="Send"/>) dispatch methods.
/// </para>
/// <para>
/// This implementation is thread-safe and can be used from any thread to marshal UI operations
/// back to the thread that created the dispatcher.
/// </para>
/// </remarks>
public sealed class UiDispatcher : IUiDispatcher
{
    private readonly SynchronizationContext _uiContext;
    private readonly int _uiThreadId;

    /// <summary>
    /// Initializes a new instance of the <see cref="UiDispatcher"/> class with the specified
    /// synchronization context.
    /// </summary>
    /// <param name="uiContext">
    /// The <see cref="SynchronizationContext"/> associated with the UI thread. This context
    /// is used to marshal operations to the UI thread. Must not be <c>null</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="uiContext"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// The constructor captures the current thread's managed thread ID, establishing it as
    /// the UI thread for subsequent <see cref="IsOnUIThread"/> checks. This dispatcher should
    /// be constructed on the UI thread to ensure correct thread identification.
    /// </remarks>
    public UiDispatcher(SynchronizationContext uiContext)
    {
        _uiContext = uiContext ?? throw new ArgumentNullException(nameof(uiContext));
        _uiThreadId = Environment.CurrentManagedThreadId;
    }

    /// <summary>
    /// Gets a value indicating whether the current thread is the UI thread to which this
    /// dispatcher is bound.
    /// </summary>
    /// <value>
    /// <c>true</c> if the current thread's managed thread ID matches the UI thread ID
    /// captured during construction; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// This property provides a fast thread check by comparing managed thread IDs without
    /// requiring synchronization context interaction. It can be used to optimize action
    /// execution by avoiding marshalling overhead when already on the UI thread.
    /// </remarks>
    public bool IsOnUIThread => Environment.CurrentManagedThreadId == _uiThreadId;

    /// <summary>
    /// Asynchronously posts an action to be executed on the UI thread. This method queues
    /// the action and returns immediately without waiting for execution to complete.
    /// </summary>
    /// <param name="action">
    /// The action to execute on the UI thread. The action will be queued to the UI thread's
    /// <see cref="SynchronizationContext"/> and executed asynchronously.
    /// </param>
    /// <remarks>
    /// <para>
    /// This method uses <see cref="SynchronizationContext.Post"/> to queue the action,
    /// which is non-blocking and suitable for fire-and-forget UI updates from background threads.
    /// </para>
    /// <para>
    /// The method does not check if the current thread is the UI thread; it always posts
    /// the action through the synchronization context. This ensures consistent asynchronous
    /// behavior regardless of the calling thread.
    /// </para>
    /// <para>
    /// This method is thread-safe and can be called from any thread.
    /// </para>
    /// </remarks>
    public void Post(Action action) => _uiContext.Post(_ => action(), null);

    /// <summary>
    /// Synchronously sends an action to be executed on the UI thread. If already on the UI thread,
    /// the action is executed inline; otherwise, the calling thread blocks until the action
    /// completes on the UI thread.
    /// </summary>
    /// <param name="action">
    /// The action to execute on the UI thread. If not already on the UI thread, the calling
    /// thread will block until this action completes execution.
    /// </param>
    /// <remarks>
    /// <para>
    /// This method provides an optimization for calls originating from the UI thread by executing
    /// the action inline without marshalling overhead. For calls from other threads, it uses
    /// <see cref="SynchronizationContext.Send"/> to synchronously dispatch the action, blocking
    /// the caller until completion.
    /// </para>
    /// <para>
    /// <b>Warning:</b> Synchronous dispatch can lead to deadlocks if the UI thread is waiting
    /// for the calling thread or if there are circular dependencies. Use <see cref="Post"/>
    /// for asynchronous, non-blocking dispatch whenever possible.
    /// </para>
    /// <para>
    /// This method is thread-safe and can be called from any thread.
    /// </para>
    /// </remarks>
    public void Send(Action action)
    {
        if (IsOnUIThread) { action(); return; }
        _uiContext.Send(_ => action(), null);
    }
}