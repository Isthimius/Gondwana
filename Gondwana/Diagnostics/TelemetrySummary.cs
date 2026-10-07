namespace Gondwana.Diagnostics;

/// <summary>How observations should be interpreted when combining windows.</summary>
public enum TelemetryMetricKind
{
    /// <summary>Independent work samples; count divided by elapsed seconds gives cadence.</summary>
    Sample,
    /// <summary>Observed state; use Last for current state, not the sum across windows.</summary>
    Gauge,
    /// <summary>Renderer-owned cumulative value; WindowDelta is relative to the previous observation.</summary>
    LifetimeCounter
}

/// <summary>Whether an observation is meaningful for its source and window.</summary>
public enum TelemetryAvailability
{
    /// <summary>The source does not implement this measurement.</summary>
    Unsupported,
    /// <summary>The measurement does not apply to this rendering path.</summary>
    NotApplicable,
    /// <summary>The measurement is supported but has no samples in this window.</summary>
    NotYetSampled,
    /// <summary>At least one observation is present.</summary>
    Available
}

/// <summary>Detached statistics for observations in one window; durations use milliseconds.</summary>
/// <param name="Availability">Support and sampling state.</param>
/// <param name="Count">Number of observations, not engine cycles unless measuring cycles.</param>
/// <param name="Sum">Sum in the measurement's units.</param>
/// <param name="Minimum">Smallest observation, or null without samples.</param>
/// <param name="Maximum">Largest observation, or null without samples.</param>
/// <param name="Last">Most recent observation, or null without samples.</param>
public sealed record TelemetrySummary(
    TelemetryAvailability Availability, long Count, double Sum,
    double? Minimum, double? Maximum, double? Last)
{
    /// <summary>Whether the observations are work samples, gauges, or cumulative counters.</summary>
    public TelemetryMetricKind Kind { get; init; }
    /// <summary>Change since the previous window's final observation; null before a baseline is established.</summary>
    public double? WindowDelta { get; init; }
    /// <summary>Monotonic observation timestamp in seconds, or null without observations.</summary>
    public double? LastObservedSeconds { get; init; }
    /// <summary>Sample-weighted arithmetic mean, or null without samples.</summary>
    public double? Mean => Count == 0 ? null : Sum / Count;

    /// <summary>Combines compatible summaries using actual sample counts.</summary>
    /// <param name="summaries">Summaries for the same measurement, in chronological order.</param>
    /// <returns>A detached aggregate; the last observation follows input order.</returns>
    public static TelemetrySummary Combine(IEnumerable<TelemetrySummary> summaries)
    {
        ArgumentNullException.ThrowIfNull(summaries);
        long count = 0;
        double sum = 0;
        double? min = null, max = null, last = null;
        double? observed = null, delta = null;
        TelemetryMetricKind? kind = null;
        var availability = TelemetryAvailability.Unsupported;
        foreach (var value in summaries)
        {
            if (kind is not null && kind != value.Kind) throw new ArgumentException("Cannot combine different metric kinds.", nameof(summaries));
            kind = value.Kind;
            if (value.Availability > availability) availability = value.Availability;
            if (value.Count == 0) continue;
            count += value.Count;
            sum += value.Sum;
            min = min is null ? value.Minimum : Math.Min(min.Value, value.Minimum!.Value);
            max = max is null ? value.Maximum : Math.Max(max.Value, value.Maximum!.Value);
            last = value.Last;
            observed = value.LastObservedSeconds;
            if (value.WindowDelta is { } change) delta = (delta ?? 0) + change;
        }
        return new(availability, count, sum, min, max, last)
        {
            Kind = kind ?? TelemetryMetricKind.Sample,
            LastObservedSeconds = observed,
            WindowDelta = delta
        };
    }
}

/// <summary>Validated shared settings; configure only when collection is inactive.</summary>
/// <remarks>
/// Individual capacity limits are also constrained by two combined budgets:
/// <c>HistoryCapacity * SourceCapacity * MetricCapacity</c> cannot exceed 1,048,576
/// configured retained metric-summary slots, and
/// <c>SourceCapacity * MetricCapacity / Interval.TotalSeconds</c> cannot exceed 262,144
/// configured metric-summary materializations per second.
/// </remarks>
public sealed record TelemetryOptions
{
    /// <summary>Maximum configured retained metric summaries across history, sources, and metrics.</summary>
    internal const long MaxRetainedMetricSummaries = 1_048_576;

    /// <summary>Maximum configured worst-case metric summaries materialized per second.</summary>
    internal const long MaxPublishedMetricSummariesPerSecond = 262_144;

    /// <summary>Minimum completed-window duration; defaults to 250 ms, allowed range 10 ms–1 minute.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMilliseconds(250);
    /// <summary>Completed windows retained; allowed range 1–1024.</summary>
    public int HistoryCapacity { get; init; } = 120;
    /// <summary>Simultaneously observed sources, including Engine; allowed range 1–64.</summary>
    public int SourceCapacity { get; init; } = 16;
    /// <summary>Measurements per source, including layer detail; allowed range 8–512.</summary>
    public int MetricCapacity { get; init; } = 128;

    /// <summary>Rejects settings that could create unbounded or excessively frequent capture.</summary>
    internal void Validate()
    {
        if (Interval < TimeSpan.FromMilliseconds(10) || Interval > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(Interval));
        if (HistoryCapacity is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(HistoryCapacity));
        if (SourceCapacity is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(SourceCapacity));
        if (MetricCapacity is < 8 or > 512) throw new ArgumentOutOfRangeException(nameof(MetricCapacity));

        long retainedMetricSummaries =
            (long)HistoryCapacity * SourceCapacity * MetricCapacity;
        if (retainedMetricSummaries > MaxRetainedMetricSummaries)
        {
            throw new ArgumentException(
                $"HistoryCapacity * SourceCapacity * MetricCapacity must not exceed {MaxRetainedMetricSummaries:N0} retained metric summaries.");
        }

        double publishedMetricSummariesPerSecond =
            (double)SourceCapacity * MetricCapacity / Interval.TotalSeconds;
        if (publishedMetricSummariesPerSecond > MaxPublishedMetricSummariesPerSecond)
        {
            throw new ArgumentException(
                $"SourceCapacity * MetricCapacity / Interval.TotalSeconds must not exceed {MaxPublishedMetricSummariesPerSecond:N0} configured metric summaries per second.");
        }
    }
}

/// <summary>Detached completed window. Sources can be observed asynchronously within its interval.</summary>
/// <param name="Generation">Reset/start generation; samples from earlier generations are rejected.</param>
/// <param name="StartedSeconds">Monotonic start time in seconds on the collector clock.</param>
/// <param name="EndedSeconds">Monotonic end time in seconds on the collector clock.</param>
/// <param name="Sources">Read-only detached source measurements.</param>
/// <param name="Truncated">Some source or metric detail exceeded configured limits.</param>
public sealed record TelemetrySnapshot(long Generation, double StartedSeconds, double EndedSeconds,
    IReadOnlyList<TelemetrySourceSnapshot> Sources, bool Truncated)
{
    /// <summary>Actual observed wall-clock duration, excluding stopped gaps.</summary>
    public double ElapsedSeconds => EndedSeconds - StartedSeconds;
}

/// <summary>Detached source identity, backend label, and measurements. No runtime objects are retained.</summary>
/// <param name="Id">Stable session-local identity, never reused.</param>
/// <param name="Name">Bounded source name.</param>
/// <param name="Backend">Backend or subsystem label.</param>
/// <param name="Metrics">Read-only measurements keyed by explicitly registered names.</param>
public sealed record TelemetrySourceSnapshot(long Id, string Name, string Backend,
    IReadOnlyDictionary<string, TelemetrySummary> Metrics);
