using Gondwana.Common;
using Gondwana.Timers;
using System.Windows.Forms;

namespace Gondwana.Input.Keyboard
{
    public struct KeyEventConfiguration
    {
        public Keys Key;
        private long _ticksBetweenEvents;
        public double TimeBetweenEvents
        {
            get { return (double)_ticksBetweenEvents / (double)HighResTimer.TicksPerSecond; }
            set { _ticksBetweenEvents = (long)(value * (double)HighResTimer.TicksPerSecond); }
        }
        
        public bool Paused;
        internal long LastKeyEvent;

        public KeyEventConfiguration(Keys key, double timeBetweenEvents, bool paused)
        {
            Key = key;
            Paused = paused;
            LastKeyEvent = 0;
            _ticksBetweenEvents = (long)(timeBetweenEvents * (double)HighResTimer.TicksPerSecond);
        }

        internal bool ReadyForNextEvent(long tick)
        {
            return (tick - LastKeyEvent >= _ticksBetweenEvents);
        }
    }
}
