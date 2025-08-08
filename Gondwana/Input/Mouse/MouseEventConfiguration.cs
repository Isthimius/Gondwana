using Gondwana.Timers;

namespace Gondwana.Input.Mouse;

public struct MouseEventConfiguration
{
    internal long _lastMouseEvent;
    private long _ticksBetweenEvents;

    public MouseEventConfiguration(bool trackMouseMovement, double timeBetweenEvents)
    {
        _lastMouseEvent = 0;
        TrackMouseMovement = trackMouseMovement;
        _ticksBetweenEvents = (long)(timeBetweenEvents * HighResTimer.TicksPerSecond);
    }

    public bool TrackMouseMovement { get; set; }

    public double TimeBetweenEvents
    {
        get => (double)_ticksBetweenEvents / HighResTimer.TicksPerSecond;
        set => _ticksBetweenEvents = (long)(value * HighResTimer.TicksPerSecond);
    }

    public bool ReadyForNextEvent(long tick) => tick - _lastMouseEvent >= _ticksBetweenEvents;
}
