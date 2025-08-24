namespace Gondwana;

public sealed class UiDispatcher : IUiDispatcher
{
    private readonly SynchronizationContext _uiContext;
    private readonly int _uiThreadId;

    public UiDispatcher(SynchronizationContext uiContext)
    {
        _uiContext = uiContext ?? throw new ArgumentNullException(nameof(uiContext));
        _uiThreadId = Environment.CurrentManagedThreadId;
    }

    public bool IsOnUIThread => Environment.CurrentManagedThreadId == _uiThreadId;

    public void Post(Action action) => _uiContext.Post(_ => action(), null);

    public void Send(Action action)
    {
        if (IsOnUIThread) { action(); return; }
        _uiContext.Send(_ => action(), null);
    }
}
