using System;
using System.Collections.Generic;

namespace Gondwana.Timers;

public sealed class Timer : IDisposable
{
    public event TimerEventHandler? Tick;

    public TimerType Type { get; }
    public TimerCycles Cycles { get; }
    public long Length { get; }
    public string TimerID { get; internal set; } = string.Empty;
    public bool Paused { get; set; }

    private long StartTick { get; }
    private long LastEventTick { get; set; }
    internal bool engineTimer;
    private bool _disposed;

    private static readonly Dictionary<string, Timer> _timers = new();
    public static bool PausedAll { get; set; }

    private Timer(TimerType type, TimerCycles cycles, long startTick, double length)
    {
        Type = type;
        Cycles = cycles;
        StartTick = startTick;
        LastEventTick = startTick;
        Length = (long)(length * HighResTimer.TicksPerSecond);
        Paused = false;
    }

    internal void RaiseTickEvent() => Tick?.Invoke(new TimerEventArgs(this));

    public void Dispose()
    {
        if (_disposed) return;
        GC.SuppressFinalize(this);
        _timers.Remove(TimerID);
        Tick = null;
        _disposed = true;
    }

    ~Timer() => Dispose();

    #region static members
    public static Timer Add(string timerID, TimerType type, TimerCycles cycles, double length)
    {
        var timer = new Timer(type, cycles, HighResTimer.GetCurrentTick(), length)
        {
            TimerID = timerID
        };
        _timers[timerID] = timer;
        return timer;
    }

    public static Timer Add(TimerType type, TimerCycles cycles, double length)
    {
        string timerID = Guid.NewGuid().ToString();
        return Add(timerID, type, cycles, length);
    }

    public static void Remove(string timerID)
    {
        if (_timers.TryGetValue(timerID, out var timer))
            timer.Dispose();
    }

    public static void Clear()
    {
        var toRemove = new List<string>();
        foreach (var (key, timer) in _timers)
        {
            if (!timer.engineTimer)
                toRemove.Add(key);
        }

        foreach (var key in toRemove)
            Remove(key);
    }

    public static Timer Get(string timerID) => _timers[timerID];

    public static void RaiseTimerEvents(TimerType type, long engineTick)
    {
        var expired = new List<string>();

        foreach (var (key, timer) in _timers)
        {
            // checking this TimerType? (i.e., PreCycle or PostCycle)
            if (timer.Type != type) continue;

            // "push" time forward for Paused Timer instances
            if (PausedAll || timer.Paused)
            {
                timer.LastEventTick += (engineTick - timer.LastEventTick);
                continue;
            }

            // check if Timer.Length time has passed
            while (engineTick - timer.LastEventTick >= timer.Length)
            {
                // save the time this event was scheduled to run
                // might be different from current system tick, but storing this value
                // will ensure that a lag in repeating timer events does not
                // accumulate over time
                timer.LastEventTick += timer.Length;
                timer.RaiseTickEvent();

                // check for any expired timers
                if (timer.Cycles == TimerCycles.Once)
                    expired.Add(key);
            }
        }

        foreach (var key in expired)
            _timers.Remove(key);
    }

    public static int Count => _timers.Count(kvp => !kvp.Value.engineTimer);

    public static string[] TimerIDs =>
        _timers.Where(kvp => !kvp.Value.engineTimer)
               .Select(kvp => kvp.Key)
               .ToArray();
    #endregion
}
