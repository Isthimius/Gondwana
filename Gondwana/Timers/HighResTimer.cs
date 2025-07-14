using System.Diagnostics;

namespace Gondwana.Timers;

/// <summary>
/// Provides utility methods for working with the system's high-resolution timer.
/// </summary>
/// <remarks>The <see cref="HighResTimer"/> class offers methods to retrieve high-resolution tick counts and
/// calculate elapsed time with precision. It relies on the <see cref="System.Diagnostics.Stopwatch"/> class to access
/// the system's high-resolution performance counter, if available.</remarks>
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
    public static long GetCurrentTick()
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
        return GetDuration(start, GetCurrentTick());
    }
}
