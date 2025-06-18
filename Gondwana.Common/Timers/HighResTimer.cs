using System.Diagnostics;

namespace Gondwana;

public static class HighResTimer
{
    /// <summary>
    /// The number of ticks per second for the system's high-resolution timer.
    /// </summary>
    public static long TicksPerSecond => Stopwatch.Frequency;

    /// <summary>
    /// Indicates whether a high-resolution performance counter is available.
    /// </summary>
    public static bool HighPerfSupported => Stopwatch.IsHighResolution;

    /// <summary>
    /// Gets the current tick count using the high-resolution timer.
    /// </summary>
    public static long GetCurrentTickCount()
    {
        return Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Returns the elapsed time in seconds between two tick counts.
    /// </summary>
    public static double GetDuration(long start, long stop)
    {
        return (double)(stop - start) / TicksPerSecond;
    }

    /// <summary>
    /// Returns the elapsed time in seconds since the given start tick.
    /// </summary>
    public static double GetElapsedSince(long start)
    {
        return GetDuration(start, GetCurrentTickCount());
    }
}
