namespace Gondwana.Tooling.Editing;

/// <summary>A reversible, document-local operation. Undo must also restore a partially failed Execute.</summary>
public interface IEditCommand
{
    /// <summary>False for a known no-op; such commands are neither executed nor retained.</summary>
    bool HasChanges => true;
    /// <summary>User-facing operation name.</summary>
    string Description { get; }
    /// <summary>Applies the new state without recording another command.</summary>
    void Execute();
    /// <summary>Restores the previous state without recording another command.</summary>
    void Undo();
}

/// <summary>Bounded, single-owner history with stable save checkpoints and atomic grouped edits.</summary>
public sealed class EditHistory : IDisposable
{
    [ThreadStatic]
    private static EditHistory? _executing;
    private sealed record Entry(IEditCommand Command, long Before, long After);
    private readonly List<Entry> _entries = [];
    private readonly Stack<EditTransaction> _transactions = new();
    private int _position;
    private long _state, _nextState;
    private long? _saved;
    private bool _busy, _disposed;
    private int _limit;

    /// <summary>Creates a history retaining at most the specified number of committed operations.</summary>
    public EditHistory(bool initiallySaved = true, int limit = 200)
    {
        if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
        _limit = limit;
        _saved = initiallySaved ? 0 : null;
    }

    /// <summary>Raised once after an operation, replay, outer transaction, or checkpoint change.</summary>
    public event EventHandler? Changed;
    /// <summary>Whether undo is currently available.</summary>
    public bool CanUndo => !_disposed && !_busy && _transactions.Count == 0 && _position > 0;
    /// <summary>Whether redo is currently available.</summary>
    public bool CanRedo => !_disposed && !_busy && _transactions.Count == 0 && _position < _entries.Count;
    /// <summary>Description of the next undo operation.</summary>
    public string? UndoDescription => CanUndo ? _entries[_position - 1].Command.Description : null;
    /// <summary>Description of the next redo operation.</summary>
    public string? RedoDescription => CanRedo ? _entries[_position].Command.Description : null;
    /// <summary>Whether the current state differs from the last successful save.</summary>
    public bool IsDirty => _state != _saved || _transactions.Any(t => t.Commands.Count != 0);
    /// <summary>Whether replay or initial command execution is in progress.</summary>
    public bool IsExecuting => _busy;
    /// <summary>Whether an edit transaction is open.</summary>
    public bool InTransaction => _transactions.Count != 0;
    /// <summary>Maximum retained committed operations; pruning never invents a save checkpoint.</summary>
    public int Limit
    {
        get => _limit;
        set { RequireIdle(); if (value < 1) throw new ArgumentOutOfRangeException(nameof(value)); _limit = value; Trim(); Notify(); }
    }

    internal void RequireIdle(bool allowTransaction = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_executing is not null || _busy || !allowTransaction && InTransaction) throw new InvalidOperationException("History is executing or has an open transaction. Commands cannot record edits in another document.");
    }

    /// <summary>Executes and records an operation; failures restore the command and cancel open transactions.</summary>
    public void Execute(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        RequireIdle(allowTransaction: true);
        if (!command.HasChanges) return;
        try { Run(command, undo: false); }
        catch { CancelAll(); throw; }
        if (InTransaction) _transactions.Peek().Commands.Add(command);
        else { Record(command); Notify(); }
    }

    /// <summary>Executes a delegate-backed reversible operation. Delegates must capture models, not UI controls.</summary>
    public void Execute(string description, Action apply, Action undo, bool hasChanges = true) => Execute(new DelegateCommand(description, apply, undo, hasChanges));

    /// <summary>Assigns a property only when its value changes.</summary>
    public bool Set<T>(string description, Func<T> get, Action<T> set, T value)
    {
        RequireIdle(allowTransaction: true);
        var before = get();
        if (EqualityComparer<T>.Default.Equals(before, value)) return false;
        Execute(description, () => set(value), () => set(before));
        return true;
    }

    /// <summary>Reverses the last committed operation.</summary>
    public void Undo()
    {
        RequireIdle();
        if (!CanUndo) return;
        var entry = _entries[_position - 1];
        Run(entry.Command, undo: true);
        _position--; _state = entry.Before; Notify();
    }

    /// <summary>Reapplies the next committed operation.</summary>
    public void Redo()
    {
        RequireIdle();
        if (!CanRedo) return;
        var entry = _entries[_position];
        Run(entry.Command, undo: false);
        _position++; _state = entry.After; Notify();
    }

    private void Run(IEditCommand command, bool undo)
    {
        _busy = true;
        _executing = this;
        bool faulted = false;
        try { Apply(command, undo); }
        catch (RecoveryException)
        {
            _entries.Clear(); _position = 0; _saved = null;
            foreach (var transaction in _transactions) { transaction.Finished = true; transaction.Commands.Clear(); }
            _transactions.Clear();
            faulted = true;
            throw;
        }
        finally { _executing = null; _busy = false; if (faulted) Notify(); }
    }

    private static void Apply(IEditCommand command, bool undo)
    {
        if (command is GroupCommand group)
        {
            var ordered = undo ? group.Commands.Reverse().ToArray() : group.Commands;
            int completed = 0;
            try { for (; completed < ordered.Length; completed++) Apply(ordered[completed], undo); }
            catch (Exception failure)
            {
                try { for (int i = completed - 1; i >= 0; i--) Apply(ordered[i], !undo); }
                catch (Exception recovery) { throw new RecoveryException(failure, recovery); }
                throw;
            }
            return;
        }
        try { if (undo) command.Undo(); else command.Execute(); }
        catch (Exception failure)
        {
            try { if (undo) command.Execute(); else command.Undo(); }
            catch (Exception recovery) { throw new RecoveryException(failure, recovery); }
            throw;
        }
    }

    private sealed class RecoveryException(Exception failure, Exception recovery)
        : AggregateException("Edit and rollback failed; history was discarded and the document remains dirty.", failure, recovery);

    internal void Capture(Action change)
    {
        RequireIdle(allowTransaction: true);
        _busy = true;
        _executing = this;
        try { change(); }
        finally { _executing = null; _busy = false; }
    }

    private void Record(IEditCommand command)
    {
        _entries.RemoveRange(_position, _entries.Count - _position);
        var next = ++_nextState;
        _entries.Add(new(command, _state, next));
        _state = next; _position++; Trim();
    }

    private void Trim()
    {
        // Discard oldest applied operations first, then furthest redo entries.
        int remove = Math.Min(_position, Math.Max(0, _entries.Count - _limit));
        _entries.RemoveRange(0, remove); _position -= remove;
        if (_entries.Count > _limit) _entries.RemoveRange(_limit, _entries.Count - _limit);
    }

    /// <summary>Begins a nested, LIFO transaction. Dispose without Commit cancels and restores its edits.</summary>
    public EditTransaction BeginTransaction(string description)
    {
        RequireIdle(allowTransaction: true);
        var transaction = new EditTransaction(this, description);
        _transactions.Push(transaction);
        return transaction;
    }

    internal void Finish(EditTransaction transaction, bool commit)
    {
        if (transaction.Finished) return;
        RequireIdle(allowTransaction: true);
        if (!ReferenceEquals(_transactions.Peek(), transaction)) throw new InvalidOperationException("Close transactions in reverse order.");
        if (!commit && transaction.Commands.Count > 0)
        {
            var group = new GroupCommand(transaction.Description, transaction.Commands.ToArray());
            Run(group, undo: true);
        }
        _transactions.Pop(); transaction.Finished = true;
        if (commit && transaction.Commands.Count > 0)
        {
            var group = new GroupCommand(transaction.Description, transaction.Commands.ToArray());
            if (InTransaction) _transactions.Peek().Commands.Add(group);
            else Record(group);
        }
        transaction.Commands.Clear();
        if (!InTransaction) Notify();
    }

    private void CancelAll()
    {
        while (InTransaction) Finish(_transactions.Peek(), commit: false);
    }

    /// <summary>Marks the current state saved after persistence succeeds; retains undo/redo.</summary>
    public void MarkSaved() { RequireIdle(); _saved = _state; Notify(); }

    /// <summary>Invalidates history after an untracked legacy mutation, preventing a false clean state.</summary>
    public void MarkUntracked()
    {
        RequireIdle(); _entries.Clear(); _position = 0; _state = ++_nextState; Notify();
    }

    /// <summary>Releases retained commands without changing dirty-state semantics.</summary>
    public void Clear() { RequireIdle(); _entries.Clear(); _position = 0; Notify(); }
    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Cancels pending transactions, releases command references, and detaches listeners.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        RequireIdle(allowTransaction: true);
        try { CancelAll(); }
        catch { _saved = null; throw; }
        finally
        {
            foreach (var transaction in _transactions) { transaction.Finished = true; transaction.Commands.Clear(); }
            _transactions.Clear(); _entries.Clear(); Changed = null; _disposed = true;
        }
    }

    private sealed record DelegateCommand(string Description, Action Apply, Action Restore, bool HasChanges) : IEditCommand
    {
        public void Execute() => Apply();
        public void Undo() => Restore();
    }

    private sealed record GroupCommand(string Description, IEditCommand[] Commands) : IEditCommand
    {
        public void Execute() => Apply(this, undo: false);
        public void Undo() => Apply(this, undo: true);
    }
}

/// <summary>A model-edit group. Commit explicitly; disposal cancels uncommitted edits.</summary>
public sealed class EditTransaction : IDisposable
{
    private readonly EditHistory _history;
    internal readonly List<IEditCommand> Commands = [];
    internal bool Finished;
    internal string Description { get; }
    internal EditTransaction(EditHistory history, string description) { _history = history; Description = description; }
    /// <summary>Commits the group as one operation.</summary>
    public void Commit() => _history.Finish(this, commit: true);
    /// <summary>Restores all edits in the group.</summary>
    public void Cancel() => _history.Finish(this, commit: false);
    /// <summary>Cancels an uncommitted group.</summary>
    public void Dispose() => Cancel();
}
