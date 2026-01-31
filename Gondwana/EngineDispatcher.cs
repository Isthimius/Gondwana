using System.Collections.Concurrent;

namespace Gondwana;

public sealed class EngineDispatcher : IEngineDispatcher
{
    private readonly ConcurrentQueue<Action> _queue = new();
    private int _engineThreadId;

    public void BindToCurrentThread()
        => _engineThreadId = Environment.CurrentManagedThreadId;

    public bool IsOnEngineThread
        => Environment.CurrentManagedThreadId == _engineThreadId;

    public void Post(Action action)
    {
        if (action is null) return;

        // Optional: run inline if already on engine thread.
        if (IsOnEngineThread) { action(); return; }

        _queue.Enqueue(action);
    }

    public void Drain()
    {
        while (_queue.TryDequeue(out var a))
            a();
    }
}
