using Gondwana.Input.Touch;

namespace Gondwana.WinForms.Input.Touch;

/// <summary>
/// Thread-safe contact state shared by WinForms UI messages and the engine's touch poller.
/// Keeps complete begin/end queues so a fast tap is not lost between engine ticks.
/// </summary>
internal sealed class WinFormsTouchState
{
    private readonly object _sync = new();
    private readonly Dictionary<int, TouchPoint> _active = [];
    private readonly Queue<TouchPoint> _began = new();
    private readonly Queue<TouchPoint> _ended = [];

    internal IReadOnlyList<TouchPoint> ActiveTouches
    {
        get
        {
            lock (_sync)
                return _active.Values.ToArray();
        }
    }

    internal void Begin(int id, System.Drawing.Point position)
    {
        lock (_sync)
        {
            if (_active.ContainsKey(id))
                return;

            var point = new TouchPoint(id, position, TouchPhase.Began);
            _active.Add(id, point);
            _began.Enqueue(point);
        }
    }

    internal void Move(int id, System.Drawing.Point position)
    {
        lock (_sync)
        {
            if (_active.ContainsKey(id))
                _active[id] = new TouchPoint(id, position, TouchPhase.Moved);
        }
    }

    internal void End(int id, System.Drawing.Point position, bool cancelled = false)
    {
        lock (_sync)
        {
            if (!_active.Remove(id))
                return;

            _ended.Enqueue(new TouchPoint(id, position,
                cancelled ? TouchPhase.Cancelled : TouchPhase.Ended));
        }
    }

    internal void CancelAll()
    {
        lock (_sync)
        {
            foreach (TouchPoint point in _active.Values)
                _ended.Enqueue(point with { Phase = TouchPhase.Cancelled });

            _active.Clear();
        }
    }

    internal IReadOnlyList<TouchPoint> ConsumeBeginnings()
    {
        lock (_sync)
        {
            TouchPoint[] snapshot = _began.ToArray();
            _began.Clear();
            return snapshot;
        }
    }

    internal IReadOnlyList<TouchPoint> ConsumeEndings()
    {
        lock (_sync)
        {
            TouchPoint[] snapshot = _ended.ToArray();
            _ended.Clear();
            return snapshot;
        }
    }

    /// <summary>
    /// Windows adds the MI_WP_SIGNATURE to mouse messages promoted from touch or pen.
    /// Bit 7 differentiates touch from pen; genuine mouse and pen input must remain intact.
    /// </summary>
    internal static bool IsPromotedTouchMouseMessage(long extraInfo)
    {
        uint marker = unchecked((uint)extraInfo);
        return (marker & 0xFFFFFF00u) == 0xFF515700u &&
               (marker & 0x80u) != 0;
    }
}
