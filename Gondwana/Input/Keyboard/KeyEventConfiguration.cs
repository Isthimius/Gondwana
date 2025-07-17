using Gondwana.Timers;

namespace Gondwana.Input.Keyboard;

public struct KeyEventConfiguration
{
    public string Key; // Could be "A", "Enter", "ArrowUp", etc.
    public bool Paused;
    internal long LastKeyEvent;
    private long _ticksBetweenEvents;

    public KeyEventConfiguration(string key, double timeBetweenEvents = 0, bool paused = false)
    {
        Key = key;
        Paused = paused;
        LastKeyEvent = 0;
        _ticksBetweenEvents = (long)(timeBetweenEvents * HighResTimer.TicksPerSecond);
    }

    public double TimeBetweenEvents
    {
        get => (double)_ticksBetweenEvents / HighResTimer.TicksPerSecond;
        set => _ticksBetweenEvents = (long)(value * HighResTimer.TicksPerSecond);
    }

    public bool ReadyForNextEvent(long tick) => tick - LastKeyEvent >= _ticksBetweenEvents;
}
