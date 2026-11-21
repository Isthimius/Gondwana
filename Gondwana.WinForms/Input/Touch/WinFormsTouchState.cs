using Gondwana.Input.Touch;

namespace Gondwana.WinForms.Input.Touch;

/// <summary>
/// Thread-safe Windows touch contact buffer. A poll reads a coherent prefix of
/// chronological native transitions, never two successive lifecycles for the
/// same contact ID in one tick. This preserves rapid taps and ID reuse with
/// the existing began/ended polling contract.
/// </summary>
internal sealed class WinFormsTouchState : ITouchAdapter
{
    private readonly object _sync = new();
    private readonly Dictionary<int, TouchPoint> _nativeActive = [];
    private readonly Dictionary<int, TouchPoint> _reportedActive = [];
    private readonly Queue<TouchPoint> _pending = [];
    private TouchPoint[] _batchBeginnings = [];
    private TouchPoint[] _batchEndings = [];
    private TouchPoint[] _snapshot = [];

    /// <summary>
    /// The contact snapshot corresponding to the last consumed lifecycle
    /// batch, not newer native events that have not yet been handed to the
    /// poller. This prevents phantom rediscovery of deferred contacts.
    /// </summary>
    public IReadOnlyList<TouchPoint> ActiveTouches
    {
        get
        {
            lock (_sync)
                return _snapshot;
        }
    }

    internal void Begin(int id, System.Drawing.Point position)
    {
        lock (_sync)
        {
            if (_nativeActive.ContainsKey(id))
                return;

            var point = new TouchPoint(id, position, TouchPhase.Began);
            _nativeActive.Add(id, point);
            _pending.Enqueue(point);
        }
    }

    internal void Move(int id, System.Drawing.Point position)
    {
        lock (_sync)
        {
            if (!_nativeActive.ContainsKey(id))
                return;

            var point = new TouchPoint(id, position, TouchPhase.Moved);
            _nativeActive[id] = point;
            _pending.Enqueue(point);
        }
    }

    internal void End(int id, System.Drawing.Point position, bool cancelled = false)
    {
        lock (_sync)
        {
            if (!_nativeActive.Remove(id))
                return;

            _pending.Enqueue(new TouchPoint(id, position,
                cancelled ? TouchPhase.Cancelled : TouchPhase.Ended));
        }
    }

    internal void CancelAll()
    {
        lock (_sync)
        {
            foreach (TouchPoint point in _nativeActive.Values)
                _pending.Enqueue(point with { Phase = TouchPhase.Cancelled });

            _nativeActive.Clear();
        }
    }

    /// <summary>
    /// Starts an atomic snapshot for one engine poll. Native callbacks after
    /// this call append to the pending queue and cannot affect the endings or
    /// active contacts returned for the current poll.
    /// </summary>
    public IReadOnlyList<TouchPoint> ConsumeBeganTouches()
    {
        lock (_sync)
        {
            var beginnings = new List<TouchPoint>();
            var endings = new List<TouchPoint>();
            bool reachedEnding = false;

            // The original poller emits all beginnings before all endings.
            // Stop before the next beginning after any ending, so separate
            // taps never look like simultaneous fingers. This also prevents
            // a reused native ID being deduplicated within a single tick.
            while (_pending.Count > 0)
            {
                TouchPoint next = _pending.Peek();
                if (reachedEnding && next.Phase == TouchPhase.Began)
                    break;

                _pending.Dequeue();
                switch (next.Phase)
                {
                    case TouchPhase.Began:
                        beginnings.Add(next);
                        _reportedActive[next.Id] = next;
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Cancelled:
                        endings.Add(next);
                        _reportedActive.Remove(next.Id);
                        reachedEnding = true;
                        break;
                    case TouchPhase.Moved:
                        if (_reportedActive.ContainsKey(next.Id))
                            _reportedActive[next.Id] = next;
                        break;
                }
            }

            _batchBeginnings = beginnings.ToArray();
            _batchEndings = endings.ToArray();
            _snapshot = _reportedActive.Values.ToArray();
            return _batchBeginnings;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<TouchPoint> ConsumeEndedTouches()
    {
        lock (_sync)
        {
            TouchPoint[] result = _batchEndings;
            _batchEndings = [];
            _batchBeginnings = [];
            return result;
        }
    }

    // Existing internal names retained for direct contact-state unit tests.
    internal IReadOnlyList<TouchPoint> ConsumeBeginnings() => ConsumeBeganTouches();
    internal IReadOnlyList<TouchPoint> ConsumeEndings() => ConsumeEndedTouches();

    /// <summary>
    /// Windows adds MI_WP_SIGNATURE to synthesized pointer mouse messages.
    /// The touch bit distinguishes fingers from pen and genuine mouse input.
    /// </summary>
    internal static bool IsPromotedTouchMouseMessage(long extraInfo)
    {
        uint marker = unchecked((uint)extraInfo);
        return (marker & 0xFFFFFF00u) == 0xFF515700u &&
               (marker & 0x80u) != 0;
    }
}
