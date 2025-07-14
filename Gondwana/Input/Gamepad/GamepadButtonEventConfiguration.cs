using Gondwana.Timers;

namespace Gondwana.Input.Gamepad;

public struct GamepadButtonEventConfiguration
{
    public string Button;
    public bool Paused;
    internal long LastEventTick;
    private long _ticksBetweenEvents;

    public GamepadButtonEventConfiguration(string button, double timeBetweenEvents, bool paused)
    {
        Button = button;
        Paused = paused;
        LastEventTick = 0;
        _ticksBetweenEvents = (long)(timeBetweenEvents * HighResTimer.TicksPerSecond);
    }

    public double TimeBetweenEvents
    {
        get => (double)_ticksBetweenEvents / HighResTimer.TicksPerSecond;
        set => _ticksBetweenEvents = (long)(value * HighResTimer.TicksPerSecond);
    }

    public bool ReadyForNextEvent(long tick) => tick - LastEventTick >= _ticksBetweenEvents;
}
