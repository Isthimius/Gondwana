using Gondwana.Common.Enums;

namespace Gondwana.Timers;

public static class Timers
{
    #region private fields
    internal static Dictionary<string, Timer> _timers;
    #endregion

    #region static constructor
    static Timers()
    {
        _timers = new Dictionary<string, Timer>();
        Paused = false;
    }
    #endregion

    #region public static methods
    public static Timer Add(string timerID, TimerType timerType, TimerCycles timerCycles, double timerLength)
    {
        Timer timer = new Timer(timerType, timerCycles, HighResTimer.GetCurrentTickCount(), timerLength);
        _timers.Add(timerID, timer);
        timer.TimerID = timerID;
        return timer;
    }

    public static Timer Add(TimerType timerType, TimerCycles timerCycles, double timerLength)
    {
        string timerID = Guid.NewGuid().ToString();
        Timer timer = Add(timerID, timerType, timerCycles, timerLength);
        timer.TimerID = timerID;
        return timer;
    }

    public static void Remove(string timerID)
    {
        _timers[timerID].Dispose();
    }

    public static void Clear()
    {
        List<string> keys = new List<string>();

        foreach (KeyValuePair<string, Timer> timer in _timers)
        {
            if (!timer.Value.engineTimer)
                keys.Add(timer.Key);
        }

        foreach (string key in keys)
            Remove(key);
    }

    public static Timer GetTimer(string timerID)
    {
        return _timers[timerID];
    }

    public static void RaiseTimerEvents(TimerType timerType, long engineTick)
    {
        List<string> expiredTimers = new List<string>();

        foreach (KeyValuePair<string, Timer> timer in _timers)
        {
            // checking this TimerType? (i.e., PreCycle or PostCycle)
            if (timer.Value.Type == timerType)
            {
                // "push" time forward for Paused Timer instances
                if (Paused || timer.Value.Paused)
                {
                    timer.Value.LastEventTick += (engineTick - timer.Value.LastEventTick);
                    continue;
                }

                // check if Timer.Length time has passed
                while (engineTick - timer.Value.LastEventTick >= timer.Value.Length)
                {
                    // save the time this event was scheduled to run
                    // might be different from engineTick, but storing this value
                    // will ensure that a lag in repeating timer events does not
                    // accumulate over time
                    timer.Value.LastEventTick += timer.Value.Length;

                    // raise the event
                    timer.Value.RaiseTick();

                    // check for any expired timers
                    if (timer.Value.Cycles == TimerCycles.Once)
                        expiredTimers.Add(timer.Key);
                }
            }
        }

        // remove any expired timers
        foreach (string expired in expiredTimers)
            _timers.Remove(expired);
    }
    #endregion

    #region public static properties
    public static int Count
    {
        get
        {
            int iCount = 0;
            
            foreach (KeyValuePair<string, Timer> timer in _timers)
            {
                if (!timer.Value.engineTimer)
                    iCount++;
            }

            return iCount;
        }
    }

    public static string[] TimerIDs
    {
        get
        {
            List<string> keys = new List<string>();

            foreach (KeyValuePair<string, Timer> timer in _timers)
            {
                if (!timer.Value.engineTimer)
                    keys.Add(timer.Key);
            }

            return keys.ToArray();
        }
    }

    public static bool Paused { get; set; }
    #endregion
}
