using System.Collections.Concurrent;

namespace Gondwana.Timers;

/// <summary>
/// Represents a high-resolution timer that can trigger events at specified intervals and supports various timer types
/// and cycles. This class is designed to be used within the Gondwana engine.
/// </summary>
/// <remarks>The <see cref="Timer"/> class provides functionality for creating and managing timers that can
/// trigger events based on a specified duration and cycle type. Timers can be paused, resumed, and disposed of when no
/// longer needed. The class also supports managing multiple timers through static methods, such as adding, removing,
/// and retrieving timers by their unique identifiers. This class is thread-safe for managing timers but does not
/// guarantee thread safety for individual timer instances. Use appropriate synchronization if accessing instance
/// members from multiple threads.</remarks>
public sealed class Timer : IDisposable
{
    /// <summary>
    /// Occurs when the timer interval has elapsed.
    /// </summary>
    /// <remarks>This event is raised each time the timer completes its interval.  Subscribers can handle this
    /// event to execute custom logic at regular intervals. Ensure the timer is started and enabled for the event to be
    /// raised.</remarks>
    public event TimerEventHandler? Tick;

    /// <summary>
    /// Gets the type of the timer, indicating whether it is a pre-cycle or post-cycle timer.
    /// </summary>
    public TimerType Type { get; }

    /// <summary>
    /// Gets the current timer cycles, representing whether the timer is set to run once or repeatedly.
    /// </summary>
    public TimerCycles Cycles { get; }

    /// <summary>
    /// Gets the length of the current timer interval in seconds.
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// Gets the unique identifier for the Timer.
    /// </summary>
    public string TimerID { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the Timer is currently paused.
    /// </summary>
    public bool Paused { get; set; }

    private long _lastEventTick { get; set; }
    private bool _disposed;

    private Timer(TimerType type, TimerCycles cycles, long startTick, double length)
    {
        Type = type;
        Cycles = cycles;
        _lastEventTick = startTick;
        Length = (long)(length * HighResTimer.TicksPerSecond);
        Paused = false;
    }

    internal void RaiseTickEvent() => Tick?.Invoke(new TimerEventArgs(this));

    public void Dispose()
    {
        if (_disposed) return;
        GC.SuppressFinalize(this);
        _timers.TryRemove(TimerID, out _);
        Tick = null;
        _disposed = true;
    }

    ~Timer() => Dispose();

    #region static members

    private static readonly ConcurrentDictionary<string, Timer> _timers = new();

    /// <summary>
    /// Gets or sets a value indicating whether all operations are globally paused.
    /// </summary>
    public static bool PausedAll { get; set; }

    /// <summary>
    /// Creates a new timer with the specified parameters and adds it to the collection of active timers.
    /// </summary>
    /// <remarks>The created timer is automatically added to the internal collection of active timers and can
    /// be retrieved or managed using its <paramref name="timerID"/>.</remarks>
    /// <param name="timerID">A unique identifier for the timer. Cannot be null or empty.</param>
    /// <param name="type">Gets the type of the timer, indicating whether it is a pre-cycle or post-cycle timer.</param>
    /// <param name="cycles">Gets the current timer cycles, representing whether the timer is set to run once or repeatedly.</param>
    /// <param name="length">The duration of the timer in seconds.</param>
    /// <returns>The newly created <see cref="Timer"/> instance.</returns>
    public static Timer Add(string timerID, TimerType type, TimerCycles cycles, double length)
    {
        var timer = new Timer(type, cycles, HighResTimer.GetCurrentTick(), length)
        {
            TimerID = timerID
        };
        _timers[timerID] = timer;
        return timer;
    }

    /// <summary>
    /// Creates and adds a new timer with the specified type, cycle count, and duration.
    /// </summary>
    /// <param name="type">Gets the type of the timer, indicating whether it is a pre-cycle or post-cycle timer.</param>
    /// <param name="cycles">Gets the current timer cycles, representing whether the timer is set to run once or repeatedly.</param>
    /// <param name="length">The duration of the timer in seconds.</param>
    /// <returns>A <see cref="Timer"/> instance representing the newly created timer.</returns>
    public static Timer Add(TimerType type, TimerCycles cycles, double length)
    {
        string timerID = Guid.NewGuid().ToString();
        return Add(timerID, type, cycles, length);
    }

    /// <summary>
    /// Removes the timer associated with the specified timer ID and releases its resources.
    /// </summary>
    /// <remarks>If a timer with the specified <paramref name="timerID"/> exists, it is disposed and removed
    /// from the collection. If no timer is found for the given ID, the method performs no action.</remarks>
    /// <param name="timerID">The unique identifier of the timer to remove. Cannot be null or empty.</param>
    public static void Remove(string timerID)
    {
        if (_timers.TryRemove(timerID, out var timer))
            timer.Dispose();
    }

    /// <summary>
    /// Retrieves the <see cref="Timer"/> instance associated with the specified timer ID.
    /// </summary>
    /// <param name="timerID">The unique identifier of the timer to retrieve. Cannot be <see langword="null"/> or empty.</param>
    /// <returns>The <see cref="Timer"/> instance associated with the specified timer ID.</returns>
    public static Timer Get(string timerID) => _timers[timerID];

    /// <summary>
    /// Gets the total number of active timers.
    /// </summary>
    public static int Count => _timers.Count;

    /// <summary>
    /// Gets an array of timer identifiers currently managed by the Engine.
    /// </summary>
    public static string[] TimerIDs => _timers.Keys.ToArray();

    internal static void ClearAll()
    {
        foreach (var key in _timers.Keys.ToArray())
            Remove(key);
    }

    internal static void RaiseTimerEvents(TimerType type, long engineTick)
    {
        var expired = new List<string>();

        foreach (var (key, timer) in _timers.ToArray())
        {
            // checking this TimerType (i.e., PreCycle or PostCycle)
            if (timer.Type != type) continue;

            // "push" time forward for Paused Timer instances
            if (PausedAll || timer.Paused)
            {
                timer._lastEventTick += (engineTick - timer._lastEventTick);
                continue;
            }

            // check if Timer.Length time has passed
            while (engineTick - timer._lastEventTick >= timer.Length)
            {
                // save the time this event was scheduled to run
                // might be different from current system tick, but storing this value
                // will ensure that a lag in repeating timer events does not
                // accumulate over time
                timer._lastEventTick += timer.Length;
                timer.RaiseTickEvent();

                // check for any expired timers
                if (timer.Cycles == TimerCycles.Once)
                    expired.Add(key);
            }
        }

        foreach (var key in expired)
            _timers.TryRemove(key, out _);
    }
    #endregion
}
