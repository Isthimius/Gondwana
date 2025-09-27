namespace Gondwana.Timers;

public delegate void TimerEventHandler(TimerEventArgs e);

public class TimerEventArgs : EventArgs
{
    public Timer GondwanaTimer;

    protected internal TimerEventArgs(Timer timer)
    {
        GondwanaTimer = timer;
    }
}