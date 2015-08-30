
namespace Gondwana.Common.EventArgs
{
    public delegate void TimerEventHandler(TimerEventArgs e);

    public class TimerEventArgs : System.EventArgs
    {
        public Timers.Timer GondwanaTimer;

        protected internal TimerEventArgs(Timers.Timer timer)
        {
            GondwanaTimer = timer;
        }
    }
}
