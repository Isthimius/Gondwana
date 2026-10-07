using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Gondwana.Diagnostics;

/// <summary>Opt-in, bounded runtime measurements independent of legacy CPS notifications.</summary>
/// <remarks>Queries are non-consuming. Producers take a short lock; no callbacks run inside it.
/// A completed bucket is published on the next observation or query after the interval expires.
/// Empty time spans form one idle bucket instead of an unbounded catch-up sequence.</remarks>
public sealed class RuntimeProfiler : IDisposable
{
    private readonly object _sync = new();
    private readonly Func<double> _clock;
    private readonly Dictionary<long, TelemetrySource> _sources = [];
    private Queue<TelemetrySnapshot> _history = new(120);
    private TelemetrySnapshot? _latestSnapshot;
    private TelemetryOptions _options = new();
    private long _nextId, _generation;
    private int _requests;
    private bool _disposed, _truncated;
    private double _started;

    /// <summary>Creates an independent collector using the monotonic Stopwatch clock.</summary>
    public RuntimeProfiler() : this(() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency) { }

    /// <summary>Creates a collector with a controlled monotonic clock for deterministic tests.</summary>
    /// <param name="clock">Monotonic time in seconds.</param>
    internal RuntimeProfiler(Func<double> clock) => _clock = clock;

    /// <summary>Whether any collection request is active; safe on producer threads.</summary>
    public bool IsCollecting => Volatile.Read(ref _requests) != 0;

    /// <summary>Current immutable settings.</summary>
    public TelemetryOptions Options { get { lock (_sync) return _options; } }

    /// <summary>Changes shared settings while inactive, clearing previous measurements.</summary>
    /// <param name="options">Validated bounded settings.</param>
    public void Configure(TelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_requests != 0) throw new InvalidOperationException("Configure telemetry while inactive.");
            if (_sources.Count > options.SourceCapacity || _sources.Values.Any(s => s.Metrics.Count > options.MetricCapacity))
                throw new InvalidOperationException("Configured limits are smaller than registered detail.");
            _options = options;
            _history = new Queue<TelemetrySnapshot>(options.HistoryCapacity);
            ResetCore();
        }
    }

    /// <summary>Starts or joins collection. Disposing the last request stops collection.</summary>
    /// <returns>An idempotent independent request; dispose it during consumer shutdown.</returns>
    public IDisposable Start()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_requests == 0)
            {
                _generation++;
                _started = _clock();
                ClearAccumulators();
                foreach (var source in _sources.Values)
                    foreach (var accumulator in source.Metrics.Values) accumulator.Rebase();
            }
            _requests++;
            return new Request(this);
        }
    }

    /// <summary>Registers an explicitly named source without retaining any runtime object.</summary>
    /// <param name="name">Nonempty name of at most 96 characters.</param>
    /// <param name="backend">Nonempty backend label of at most 96 characters.</param>
    /// <returns>A source handle, or null when the source limit is reached; truncation is reported.</returns>
    public TelemetrySource? RegisterSource(string name, string backend)
    {
        ValidateName(name);
        ValidateName(backend);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_sources.Count >= _options.SourceCapacity) { _truncated = true; return null; }
            var source = new TelemetrySource(this, ++_nextId, name, backend);
            _sources.Add(source.Id, source);
            return source;
        }
    }

    /// <summary>Attaches optional engine instrumentation without changing host construction after shutdown.</summary>
    /// <param name="name">Bounded source label.</param>
    /// <param name="backend">Bounded backend label.</param>
    /// <returns>A registered source, or null if the collector is disposed or full.</returns>
    internal TelemetrySource? TryRegisterSource(string name, string backend)
    {
        lock (_sync)
            return _disposed ? null : RegisterSource(name, backend);
    }

    /// <summary>Returns the latest completed bucket, or null before one completes.</summary>
    /// <returns>An immutable detached snapshot safe to retain across reset and disposal.</returns>
    public TelemetrySnapshot? GetLatestSnapshot()
    {
        lock (_sync)
        {
            Advance();
            return _latestSnapshot;
        }
    }

    /// <summary>Returns bounded completed buckets in chronological order without consuming them.</summary>
    /// <returns>A detached read-only collection.</returns>
    public IReadOnlyList<TelemetrySnapshot> GetHistory()
    {
        lock (_sync)
        {
            Advance();
            return Array.AsReadOnly(_history.ToArray());
        }
    }

    /// <summary>Clears telemetry history and rebases observations without changing renderer counters or leases.</summary>
    public void Reset() { lock (_sync) { ObjectDisposedException.ThrowIf(_disposed, this); ResetCore(); } }

    /// <summary>Stops collection, invalidates in-flight samples, and retires all source handles.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _requests = 0;
            _generation++;
            _sources.Clear();
            _history.Clear();
            _latestSnapshot = null;
        }
    }

    /// <summary>Captures a generation token; zero means collection is inactive.</summary>
    /// <returns>A generation to capture before work begins.</returns>
    internal long Begin() => IsCollecting ? Volatile.Read(ref _generation) : 0;

    /// <summary>Registers bounded metric detail; registration is cold-path work.</summary>
    /// <param name="source">Registered neutral source.</param>
    /// <param name="name">Bounded metric key including units.</param>
    /// <param name="availability">Support state before sampling.</param>
    /// <param name="kind">Observation interpretation.</param>
    /// <returns>Whether the definition was retained.</returns>
    internal bool Define(TelemetrySource source, string name, TelemetryAvailability availability, TelemetryMetricKind kind)
    {
        ValidateName(name);
        lock (_sync)
        {
            if (!_sources.ContainsKey(source.Id)) return false;
            if (source.Metrics.TryGetValue(name, out var existing))
            {
                if (existing.Availability == TelemetryAvailability.Unsupported && availability == TelemetryAvailability.NotYetSampled)
                    existing.Availability = availability;
                return true;
            }
            if (source.Metrics.Count >= _options.MetricCapacity) { _truncated = true; return false; }
            source.Metrics.Add(name, new Accumulator { Availability = availability, Kind = kind });
            return true;
        }
    }

    /// <summary>Accepts one allocation-free observation if its source and generation remain active.</summary>
    /// <param name="source">Registered neutral source.</param>
    /// <param name="generation">Epoch captured before work.</param>
    /// <param name="metric">Previously registered key.</param>
    /// <param name="value">Finite observation in declared units.</param>
    internal void Record(TelemetrySource source, long generation, string metric, double value)
    {
        if (generation == 0 || !IsCollecting || !double.IsFinite(value)) return;
        lock (_sync)
        {
            if (_requests == 0 || generation != _generation || !_sources.ContainsKey(source.Id)) return;
            Advance();
            if (!source.Metrics.TryGetValue(metric, out var accumulator)) return;
            if (accumulator.Availability is TelemetryAvailability.Unsupported or TelemetryAvailability.NotApplicable) return;
            accumulator.Add(value, _clock());
        }
    }

    /// <summary>Retires a source; previously detached history stays readable.</summary>
    /// <param name="source">Source whose subsequent observations must be ignored.</param>
    internal void Retire(TelemetrySource source) { lock (_sync) _sources.Remove(source.Id); }

    /// <summary>Marks omitted detail for the current collection generation.</summary>
    /// <param name="generation">Epoch of the omitted observations.</param>
    internal void MarkTruncated(long generation)
    {
        lock (_sync) { if (generation == _generation && _requests != 0) _truncated = true; }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 96) throw new ArgumentException("Names must contain 1–96 characters.", nameof(name));
    }

    private void ResetCore()
    {
        _generation++;
        _history.Clear();
        _latestSnapshot = null;
        _started = _clock();
        ClearAccumulators();
        foreach (var source in _sources.Values)
            foreach (var accumulator in source.Metrics.Values) accumulator.Rebase();
    }

    private void ClearAccumulators()
    {
        foreach (var source in _sources.Values)
            foreach (var accumulator in source.Metrics.Values) accumulator.Clear();
    }

    private void Advance()
    {
        if (_requests == 0) return;
        double now = _clock();
        if (now - _started >= _options.Interval.TotalSeconds) Publish(now);
    }

    private void Publish(double now)
    {
        var sources = _sources.Values.Select(source => new TelemetrySourceSnapshot(source.Id, source.Name, source.Backend,
            new ReadOnlyDictionary<string, TelemetrySummary>(source.Metrics.ToDictionary(p => p.Key, p => p.Value.Snapshot())))).ToArray();
        if (_history.Count == _options.HistoryCapacity) _history.Dequeue();
        _latestSnapshot = new(_generation, _started, now, Array.AsReadOnly(sources), _truncated);
        _history.Enqueue(_latestSnapshot);
        ClearAccumulators();
        _started = now;
    }

    private void Stop()
    {
        lock (_sync)
        {
            if (_requests == 0) return;
            if (_requests == 1)
            {
                double now = _clock();
                if (now > _started) Publish(now);
                _generation++;
            }
            _requests--;
        }
    }

    private sealed class Request(RuntimeProfiler owner) : IDisposable
    {
        private RuntimeProfiler? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Stop();
    }

    /// <summary>Fixed-size sufficient statistics; no raw samples are retained.</summary>
    internal sealed class Accumulator
    {
        /// <summary>Support state used when a window has no observations.</summary>
        internal TelemetryAvailability Availability;
        /// <summary>Interpretation for cumulative values and gauges.</summary>
        internal TelemetryMetricKind Kind;
        private long _count;
        private double _sum, _min, _max, _last, _observed;
        private double? _baseline;
        /// <summary>Adds a finite observation under the collector lock.</summary>
        /// <param name="value">Observation in the defined units.</param>
        /// <param name="observed">Monotonic observation time in seconds.</param>
        internal void Add(double value, double observed)
        {
            _min = _count == 0 ? value : Math.Min(_min, value);
            _max = _count == 0 ? value : Math.Max(_max, value);
            _count++;
            _sum += value;
            _last = value;
            _observed = observed;
        }
        /// <summary>Starts a new window, preserving the cumulative-counter baseline.</summary>
        internal void Clear() { if (_count > 0) _baseline = _last; _count = 0; _sum = 0; }
        /// <summary>Discards the counter baseline after reset or a collection gap.</summary>
        internal void Rebase() => _baseline = null;
        /// <summary>Copies sufficient statistics into an immutable value.</summary>
        /// <returns>Detached observations, support state, and timestamp.</returns>
        internal TelemetrySummary Snapshot() => new(_count == 0 ? Availability : TelemetryAvailability.Available,
            _count, _sum, _count == 0 ? null : _min, _count == 0 ? null : _max, _count == 0 ? null : _last)
        {
            Kind = Kind,
            LastObservedSeconds = _count == 0 ? null : _observed,
            WindowDelta = Kind == TelemetryMetricKind.LifetimeCounter && _count > 0 && _baseline is { } baseline ? _last - baseline : null
        };
    }
}

/// <summary>Bounded extension source. Define measurements once, capture a generation before work, then record it.</summary>
public sealed class TelemetrySource : IDisposable
{
    private readonly RuntimeProfiler _owner;
    /// <summary>Bounded accumulators; access is protected by the owner collector lock.</summary>
    internal Dictionary<string, RuntimeProfiler.Accumulator> Metrics { get; } = [];
    /// <summary>Creates a neutral handle without retaining the measured runtime object.</summary>
    /// <param name="owner">Collector that owns observations.</param>
    /// <param name="id">Never-reused session identity.</param>
    /// <param name="name">Bounded source label.</param>
    /// <param name="backend">Bounded backend label.</param>
    internal TelemetrySource(RuntimeProfiler owner, long id, string name, string backend)
        => (_owner, Id, Name, Backend) = (owner, id, name, backend);
    /// <summary>Session-local identity, unchanged by collector reset or temporary context loss.</summary>
    public long Id { get; }
    /// <summary>Source label.</summary>
    public string Name { get; }
    /// <summary>Backend label.</summary>
    public string Backend { get; }
    /// <summary>Defines a measurement before use. Include units and scope in its name.</summary>
    /// <param name="name">Bounded unique metric name, such as update.cpu.ms.</param>
    /// <param name="availability">Unsupported, NotApplicable, or NotYetSampled; Available is normalized to NotYetSampled.</param>
    /// <param name="kind">Interpretation of observations across windows.</param>
    /// <returns>False if the source is retired or configured detail capacity is exhausted.</returns>
    public bool Define(string name, TelemetryAvailability availability = TelemetryAvailability.NotYetSampled,
        TelemetryMetricKind kind = TelemetryMetricKind.Sample)
        => _owner.Define(this, name, availability == TelemetryAvailability.Available ? TelemetryAvailability.NotYetSampled : availability, kind);
    /// <summary>Reports omitted source detail without allocating a diagnostic object.</summary>
    /// <param name="generation">Epoch of the omitted observations.</param>
    internal void MarkTruncated(long generation) => _owner.MarkTruncated(generation);
    /// <summary>Captures an epoch before measured work; zero means disabled.</summary>
    /// <returns>A token to pass to Record; reset, stop, and source retirement invalidate old tokens.</returns>
    public long BeginSample() => _owner.Begin();
    /// <summary>Records a finite observation without retaining raw samples.</summary>
    /// <param name="generation">Token captured before the measured operation.</param>
    /// <param name="metric">Previously defined measurement name.</param>
    /// <param name="value">Value in the metric's declared units.</param>
    public void Record(long generation, string metric, double value) => _owner.Record(this, generation, metric, value);
    /// <summary>Permanently retires the source; late observations are ignored.</summary>
    public void Dispose() => _owner.Retire(this);
}
