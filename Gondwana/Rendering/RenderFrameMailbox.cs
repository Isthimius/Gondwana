namespace Gondwana.Rendering;

/// <summary>
/// Bounded single-producer/single-consumer mailbox. The monitor protects only slot
/// ownership. Building, replay and resource disposal always happen outside it.
/// A stalled consumer owns one slot; the producer alternates the other two.
/// </summary>
internal sealed class RenderFrameMailbox : IDisposable
{
    internal enum State
    {
        /// <summary>
        /// The slot is available for a new frame.
        /// </summary>
        Free,
        /// <summary>
        /// The producer is constructing the frame.
        /// </summary>
        Building,
        /// <summary>
        /// The completed frame is waiting for a renderer.
        /// </summary>
        Published,
        /// <summary>
        /// The renderer owns the frame.
        /// </summary>
        Rendering,
        /// <summary>
        /// The slot is releasing its frame resources.
        /// </summary>
        Releasing
    }

    internal sealed class Slot
    {
        internal RenderFrameSnapshot? Frame { get; set; }
        internal State Ownership;

        internal Slot() { }
    }

    private readonly object _gate = new();
    private readonly Slot[] _slots = [new(), new(), new()];
    private Slot? _published;
    private bool _closed;
    private long _publishedCount;
    private long _droppedCount;

    internal (long Published, long Dropped, int InUse) Counters
    {
        get
        {
            lock (_gate)
                return (_publishedCount, _droppedCount, _slots.Count(s => s.Ownership != State.Free));
        }
    }

    internal Slot? TryBeginBuild()
    {
        lock (_gate)
        {
            if (_closed) return null;
            if (_slots.Any(s => s.Ownership == State.Building))
                throw new InvalidOperationException("Only one snapshot producer is supported.");
            var slot = _slots.FirstOrDefault(s => s.Ownership == State.Free);
            if (slot is null) return null;
            slot.Ownership = State.Building;
            return slot;
        }
    }

    // Takes ownership of frame even if the mailbox was closed during construction.
    internal void Publish(Slot slot, RenderFrameSnapshot frame)
    {
        Slot? retired;
        lock (_gate)
        {
            Require(slot, State.Building);
            slot.Frame = frame;
            if (_closed)
            {
                slot.Ownership = State.Releasing;
                retired = slot;
            }
            else
            {
                retired = _published;
                if (retired is not null)
                {
                    retired.Ownership = State.Releasing;
                    _droppedCount++;
                }
                slot.Ownership = State.Published;
                _published = slot;
                _publishedCount++;
            }
        }
        if (retired is not null) Recycle(retired);
    }

    internal void AbortBuild(Slot slot)
    {
        lock (_gate)
        {
            Require(slot, State.Building);
            slot.Ownership = State.Free;
        }
    }

    internal Slot? TryAcquire()
    {
        lock (_gate)
        {
            if (_closed || _published is null) return null;
            if (_slots.Any(s => s.Ownership == State.Rendering))
                throw new InvalidOperationException("Only one snapshot consumer is supported.");
            var slot = _published;
            _published = null;
            slot.Ownership = State.Rendering;
            return slot;
        }
    }

    internal void Release(Slot slot)
    {
        lock (_gate)
        {
            Require(slot, State.Rendering);
            slot.Ownership = State.Releasing;
        }
        Recycle(slot);
    }

    private void Require(Slot slot, State state)
    {
        if (!Array.Exists(_slots, candidate => ReferenceEquals(candidate, slot)) || slot.Ownership != state)
            throw new InvalidOperationException("Snapshot slot is not owned by this operation.");
    }

    private void Recycle(Slot slot)
    {
        try { slot.Frame?.Dispose(); }
        finally
        {
            lock (_gate)
            {
                slot.Frame = null;
                slot.Ownership = State.Free;
            }
        }
    }

    /// <summary>
    /// Releases the resources owned by this instance.
    /// </summary>
    public void Dispose()
    {
        Slot? retired;
        lock (_gate)
        {
            if (_closed) return;
            _closed = true;
            retired = _published;
            _published = null;
            if (retired is not null) retired.Ownership = State.Releasing;
            // Building and Rendering belong to their callers until Publish/Abort/Release.
        }
        if (retired is not null) Recycle(retired);
    }
}

