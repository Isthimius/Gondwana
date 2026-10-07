using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Text;
using Gondwana.Diagnostics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Widgets.Controls;
using SkiaSharp;

namespace Gondwana.Widgets.Hud;

/// <summary>
/// Controls the default visibility policy for runtime-profiler measurements.
/// </summary>
public enum ProfilerMeasurementVisibilityMode
{
    /// <summary>
    /// Shows every measurement unless it is explicitly hidden.
    /// </summary>
    All,

    /// <summary>
    /// Hides measurements unless they are explicitly shown.
    /// </summary>
    Selected
}

/// <summary>
/// Displays detached <see cref="RuntimeProfiler"/> snapshots as a screen-space HUD widget.
/// </summary>
/// <remarks>
/// The widget acquires one profiler collection request while shown and releases it when hidden
/// or disposed. Multiple profiler consumers remain independent. The widget only reads detached
/// snapshots; it does not inspect live scene collections or renderer objects.
/// </remarks>
public sealed class ProfilerWidget : ContainerWidget
{
    private static readonly TimeSpan MinimumRefreshInterval = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan MaximumRefreshInterval = TimeSpan.FromMinutes(1);

    private readonly Dictionary<string, bool> _sourceVisibility = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _measurementVisibility = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _sourceMeasurementVisibility = new(StringComparer.Ordinal);
    private Size _size;
    private TimeSpan _refreshInterval = TimeSpan.FromMilliseconds(250);
    private long _lastRefreshTimestamp;
    private IDisposable? _collectionRequest;
    private bool _disposed;
    private bool _showHeader = true;
    private bool _showSnapshotMetadata = true;
    private bool _showSourceHeaders = true;
    private bool _showUnavailableMeasurements;

    /// <summary>
    /// Creates a view-level runtime-profiler widget.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host that owns the widget.</param>
    /// <param name="view">The view that renders the widget.</param>
    /// <param name="bounds">The widget bounds in view pixels.</param>
    /// <param name="nickname">An optional diagnostic nickname.</param>
    public ProfilerWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        View view,
        Rectangle bounds,
        string? nickname = null)
        : base(
            renderSurfaceHost,
            DirectDrawingMode.View,
            ValidateBounds(bounds).Location,
            nickname)
    {
        ArgumentNullException.ThrowIfNull(view);

        _size = bounds.Size;

        Display = new LabelWidget(
            renderSurfaceHost,
            view,
            bounds,
            nickname: $"{Nickname}.display")
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        Display
            .SetFont(SKTypeface.Default, 15f, minSize: 9f)
            .SetColors(SKColors.White, new SKColor(0, 0, 0, 102))
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Top)
            .SetPadding(12f, 10f)
            .EnableWrapping(false);

        Add(Display, Vector2.Zero);

        IsInputEnabled = false;
        IsPointerInputEnabled = false;
        IsKeyboardInputEnabled = false;
        CanReceiveFocus = false;

        Display.SetText("Gondwana Runtime Profiler\nInactive. Call Show() to collect telemetry.");
    }

    /// <summary>
    /// Gets the label used to render profiler text and optional scrolling.
    /// </summary>
    public LabelWidget Display { get; }

    /// <summary>
    /// Gets the current widget bounds in view pixels.
    /// </summary>
    public Rectangle Bounds
    {
        get
        {
            Vector2 position = GetPosition();
            return new Rectangle(
                (int)MathF.Round(position.X),
                (int)MathF.Round(position.Y),
                _size.Width,
                _size.Height);
        }
    }

    /// <summary>
    /// Gets or sets the widget size while preserving its current position.
    /// </summary>
    public Size Size
    {
        get => _size;
        set
        {
            ValidateSize(value);
            _size = value;
            Display.Size = value;
        }
    }

    /// <summary>
    /// Gets or sets how frequently the widget reads and formats the latest completed snapshot.
    /// </summary>
    /// <remarks>
    /// This controls display refresh only. Profiler observations are still recorded at their
    /// normal instrumentation boundaries.
    /// </remarks>
    public TimeSpan RefreshInterval
    {
        get => _refreshInterval;
        set
        {
            if (value < MinimumRefreshInterval || value > MaximumRefreshInterval)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Profiler widget refresh interval must be between 10 ms and one minute.");
            }

            _refreshInterval = value;
        }
    }

    /// <summary>
    /// Gets or sets the default measurement visibility policy.
    /// </summary>
    public ProfilerMeasurementVisibilityMode MeasurementVisibilityMode { get; set; } =
        ProfilerMeasurementVisibilityMode.All;

    /// <summary>
    /// Gets or sets whether the "Gondwana Runtime Profiler" heading is displayed.
    /// </summary>
    public bool ShowHeader
    {
        get => _showHeader;
        set => _showHeader = value;
    }

    /// <summary>
    /// Gets or sets whether generation, elapsed-window, and truncation information is displayed.
    /// </summary>
    public bool ShowSnapshotMetadata
    {
        get => _showSnapshotMetadata;
        set => _showSnapshotMetadata = value;
    }

    /// <summary>
    /// Gets or sets whether source name/backend headings are displayed.
    /// </summary>
    public bool ShowSourceHeaders
    {
        get => _showSourceHeaders;
        set => _showSourceHeaders = value;
    }

    /// <summary>
    /// Gets or sets whether unsupported, not-applicable, and not-yet-sampled measurements
    /// are shown when they are otherwise visible.
    /// </summary>
    public bool ShowUnavailableMeasurements
    {
        get => _showUnavailableMeasurements;
        set => _showUnavailableMeasurements = value;
    }

    /// <summary>
    /// Gets whether this widget currently owns an active profiler collection request.
    /// </summary>
    public bool IsCollectionActive => _collectionRequest is not null;

    /// <summary>
    /// Sets whether all measurements from a named profiler source are displayed.
    /// </summary>
    /// <param name="sourceName">The profiler source name, such as "Engine" or "Render surface".</param>
    /// <param name="visible"><see langword="true"/> to show the source; otherwise, <see langword="false"/>.</param>
    /// <returns>The current widget.</returns>
    public ProfilerWidget SetSourceVisible(string sourceName, bool visible)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        _sourceVisibility[sourceName] = visible;
        return this;
    }

    /// <summary>
    /// Sets a visibility override for a metric key across all profiler sources.
    /// </summary>
    /// <param name="metricKey">The metric key, such as "cycle.cpu.ms".</param>
    /// <param name="visible"><see langword="true"/> to show the metric; otherwise, <see langword="false"/>.</param>
    /// <returns>The current widget.</returns>
    public ProfilerWidget SetMeasurementVisible(string metricKey, bool visible)
    {
        ValidateMetricKey(metricKey);
        _measurementVisibility[metricKey] = visible;
        return this;
    }

    /// <summary>
    /// Sets a visibility override for one metric on one named profiler source.
    /// </summary>
    /// <param name="sourceName">The profiler source name.</param>
    /// <param name="metricKey">The metric key.</param>
    /// <param name="visible"><see langword="true"/> to show the metric; otherwise, <see langword="false"/>.</param>
    /// <returns>The current widget.</returns>
    public ProfilerWidget SetMeasurementVisible(
        string sourceName,
        string metricKey,
        bool visible)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ValidateMetricKey(metricKey);
        _sourceMeasurementVisibility[CreateSourceMetricKey(sourceName, metricKey)] = visible;
        return this;
    }

    /// <summary>
    /// Removes all explicit source and measurement visibility overrides.
    /// </summary>
    /// <returns>The current widget.</returns>
    public ProfilerWidget ClearVisibilityOverrides()
    {
        _sourceVisibility.Clear();
        _measurementVisibility.Clear();
        _sourceMeasurementVisibility.Clear();
        return this;
    }

    /// <summary>
    /// Returns whether a metric would be displayed under the current source/metric visibility rules.
    /// </summary>
    /// <param name="sourceName">The profiler source name.</param>
    /// <param name="metricKey">The metric key.</param>
    /// <returns><see langword="true"/> when the metric is selected for display.</returns>
    public bool IsMeasurementVisible(string sourceName, string metricKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ValidateMetricKey(metricKey);
        return ResolveMeasurementVisibility(sourceName, metricKey, TelemetryAvailability.Available);
    }

    /// <summary>
    /// Sets profiler text and background colors.
    /// </summary>
    /// <param name="foreground">Text color.</param>
    /// <param name="background">Background color.</param>
    /// <returns>The current widget.</returns>
    public ProfilerWidget SetColors(SKColor foreground, SKColor background)
    {
        Display.SetColors(foreground, background);
        return this;
    }

    /// <summary>
    /// Sets the profiler typeface and text size.
    /// </summary>
    /// <param name="typeface">Typeface to use.</param>
    /// <param name="size">Primary text size.</param>
    /// <param name="minSize">Optional minimum text size.</param>
    /// <returns>The current widget.</returns>
    public ProfilerWidget SetFont(SKTypeface typeface, float size, float? minSize = null)
    {
        Display.SetFont(typeface, size, minSize);
        return this;
    }

    /// <summary>
    /// Sets the base Z-order used by the profiler label and scrollbar.
    /// </summary>
    /// <param name="zOrder">The base Z-order.</param>
    /// <returns>The current widget.</returns>
    public ProfilerWidget SetProfilerZOrder(int zOrder)
    {
        Display.SetLabelZOrder(zOrder);
        return this;
    }

    /// <summary>
    /// Immediately reads and formats the latest completed profiler snapshot.
    /// </summary>
    /// <returns>The current widget.</returns>
    public ProfilerWidget Refresh()
    {
        if (_disposed)
            return this;

        TelemetrySnapshot? snapshot = Engine.Instance.Profiler.GetLatestSnapshot();
        Display.SetText(FormatSnapshot(snapshot));
        return this;
    }

    /// <inheritdoc/>
    protected override void ProcessShown()
    {
        base.ProcessShown();

        if (_collectionRequest is not null)
            return;

        _collectionRequest = Engine.Instance.Profiler.Start();
        _lastRefreshTimestamp = 0;
        Engine.Instance.AfterBackgroundTasksExecute += OnAfterBackgroundTasksExecute;
        Refresh();
    }

    /// <inheritdoc/>
    protected override void ProcessHidden()
    {
        StopCollection();
        base.ProcessHidden();
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopCollection();
        base.Dispose();
    }

    private void OnAfterBackgroundTasksExecute()
    {
        if (_collectionRequest is null)
            return;

        long now = Stopwatch.GetTimestamp();
        if (_lastRefreshTimestamp != 0 &&
            Stopwatch.GetElapsedTime(_lastRefreshTimestamp, now) < RefreshInterval)
        {
            return;
        }

        _lastRefreshTimestamp = now;
        Refresh();
    }

    private void StopCollection()
    {
        Engine.Instance.AfterBackgroundTasksExecute -= OnAfterBackgroundTasksExecute;
        _collectionRequest?.Dispose();
        _collectionRequest = null;
        _lastRefreshTimestamp = 0;
    }

    private string FormatSnapshot(TelemetrySnapshot? snapshot)
    {
        var text = new StringBuilder();

        if (ShowHeader)
            text.AppendLine("Gondwana Runtime Profiler");

        if (snapshot is null)
        {
            text.Append("Waiting for first completed telemetry window...");
            return text.ToString();
        }

        if (ShowSnapshotMetadata)
        {
            text.Append("Generation ")
                .Append(snapshot.Generation)
                .Append(" | window ")
                .Append((snapshot.ElapsedSeconds * 1000d).ToString("0.0"))
                .Append(" ms");

            if (snapshot.Truncated)
                text.Append(" | detail truncated");

            text.AppendLine();
        }

        bool wroteSource = false;
        foreach (TelemetrySourceSnapshot source in snapshot.Sources)
        {
            if (!IsSourceVisible(source.Name))
                continue;

            var metrics = source.Metrics
                .Where(pair => ResolveMeasurementVisibility(
                    source.Name,
                    pair.Key,
                    pair.Value.Availability))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToArray();

            if (metrics.Length == 0)
                continue;

            if (wroteSource)
                text.AppendLine();

            if (ShowSourceHeaders)
            {
                text.Append(source.Name)
                    .Append(" [")
                    .Append(source.Backend)
                    .AppendLine("]");
            }

            foreach (KeyValuePair<string, TelemetrySummary> pair in metrics)
            {
                text.Append("  ")
                    .Append(pair.Key)
                    .Append(": ")
                    .AppendLine(FormatSummary(pair.Value, snapshot.ElapsedSeconds));
            }

            wroteSource = true;
        }

        if (!wroteSource)
            text.Append("No measurements selected.");

        return text.ToString().TrimEnd();
    }

    private bool IsSourceVisible(string sourceName)
    {
        return !_sourceVisibility.TryGetValue(sourceName, out bool visible) || visible;
    }

    private bool ResolveMeasurementVisibility(
        string sourceName,
        string metricKey,
        TelemetryAvailability availability)
    {
        if (!IsSourceVisible(sourceName))
            return false;

        bool selected;
        if (_sourceMeasurementVisibility.TryGetValue(
                CreateSourceMetricKey(sourceName, metricKey),
                out bool sourceMetricVisible))
        {
            selected = sourceMetricVisible;
        }
        else if (_measurementVisibility.TryGetValue(metricKey, out bool metricVisible))
        {
            selected = metricVisible;
        }
        else
        {
            selected = MeasurementVisibilityMode == ProfilerMeasurementVisibilityMode.All;
        }

        return selected &&
            (ShowUnavailableMeasurements || availability == TelemetryAvailability.Available);
    }

    private static string FormatSummary(TelemetrySummary summary, double elapsedSeconds)
    {
        if (summary.Availability != TelemetryAvailability.Available)
            return summary.Availability.ToString();

        return summary.Kind switch
        {
            TelemetryMetricKind.Gauge =>
                $"last {FormatNumber(summary.Last)}",
            TelemetryMetricKind.LifetimeCounter =>
                summary.WindowDelta is { } delta
                    ? $"last {FormatNumber(summary.Last)}, delta {FormatNumber(delta)}"
                    : $"last {FormatNumber(summary.Last)}, delta n/a",
            _ =>
                $"avg {FormatNumber(summary.Mean)}, min {FormatNumber(summary.Minimum)}, " +
                $"max {FormatNumber(summary.Maximum)}, n {summary.Count:N0}, " +
                $"{FormatRate(summary.Count, elapsedSeconds)}/s"
        };
    }

    private static string FormatRate(long count, double elapsedSeconds)
    {
        return elapsedSeconds > 0d
            ? (count / elapsedSeconds).ToString("0.0")
            : "n/a";
    }

    private static string FormatNumber(double? value)
    {
        return value?.ToString("0.###") ?? "n/a";
    }

    private static string CreateSourceMetricKey(string sourceName, string metricKey)
    {
        return $"{sourceName.ToUpperInvariant()}\u001f{metricKey}";
    }

    private static void ValidateMetricKey(string metricKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricKey);
    }

    private static Rectangle ValidateBounds(Rectangle bounds)
    {
        ValidateSize(bounds.Size);
        return bounds;
    }

    private static void ValidateSize(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), size, "Profiler widget size must be positive.");
    }
}
