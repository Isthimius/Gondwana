using Gondwana.Diagnostics;

namespace Gondwana.Examples;

/// <summary>Ordinary application usage, compiled by Gondwana.Tests without any viewer dependency.</summary>
public static class RuntimeTelemetryExample
{
    /// <summary>Starts a request to retain for the application's diagnostic session.</summary>
    /// <returns>A request that the application must dispose when finished.</returns>
    public static IDisposable StartCollection() => Engine.Instance.Profiler.Start();

    /// <summary>Formats an already detached snapshot at the application's chosen display cadence.</summary>
    /// <param name="profiler">Usually Engine.Instance.Profiler.</param>
    /// <param name="output">Application output; never called on a hot producer path.</param>
    public static void PrintLatest(RuntimeProfiler profiler, TextWriter output)
    {
        var snapshot = profiler.GetLatestSnapshot();
        if (snapshot is null) return;
        foreach (var source in snapshot.Sources)
        {
            if (source.Metrics.TryGetValue("cycle.cpu.ms", out var cycle) &&
                cycle.Availability == TelemetryAvailability.Available)
                output.WriteLine($"CPS={cycle.Count / snapshot.ElapsedSeconds:F1}; CPU mean={cycle.Mean:F3} ms");
        }
    }
}
