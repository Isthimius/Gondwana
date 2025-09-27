using Gondwana.Timers;

namespace Gondwana.Input;

public abstract class InputEventConfigurationBase
{
    protected internal long _lastEventTick;
    private long _ticksBetweenEvents;

    private InputEventConfigurationBase()
    { }

    protected InputEventConfigurationBase(double secondsBetweenEvents, bool isPaused)
    {
        _lastEventTick = 0;
        _ticksBetweenEvents = (long)(secondsBetweenEvents * HighResTimer.TicksPerSecond);
        IsPaused = isPaused;
    }

    public double TimeBetweenEvents
    {
        get => (double)_ticksBetweenEvents / HighResTimer.TicksPerSecond;
        set => _ticksBetweenEvents = (long)(value * HighResTimer.TicksPerSecond);
    }

    public bool IsPaused { get; set; } = false;

    public bool ReadyForNextEvent(long tick) => tick - _lastEventTick >= _ticksBetweenEvents;
}