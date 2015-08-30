using Gondwana.Common.Enums;
using Gondwana.Common.EventArgs;
using System;

namespace Gondwana.Common.Timers
{
    public class Timer : IDisposable
    {
        public event TimerEventHandler Tick;

        public TimerType Type;
        public TimerCycles Cycles;
        public long Length;

        internal long StartTick;
        internal long LastEventTick;
        internal bool engineTimer;

        internal Timer(TimerType timerType, TimerCycles timerCycles, long startTick, double len)
        {
            Type = timerType;
            Cycles = timerCycles;
            StartTick = startTick;
            LastEventTick = startTick;
            Length = (long)(len * (double)HighResTimer.TicksPerSecond);
            Paused = false;
        }

        ~Timer()
        {
            this.Dispose();
        }

        public string TimerID
        {
            get;
            internal set;
        }

        internal void RaiseTick()
        {
            // raise the event if anyone's listening
            if (Tick != null)
                Tick(new TimerEventArgs(this));
        }

        public bool Paused { get; set; }

        #region IDisposable Members
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Timers._timers.Remove(TimerID);
            this.Tick = null;
        }
        #endregion
    }
}
