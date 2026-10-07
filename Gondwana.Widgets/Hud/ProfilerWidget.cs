using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Text;
using Gondwana.Diagnostics;
using Gondwana.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Overlays;
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
/// Selects optional non-profiler runtime context rendered alongside telemetry measurements.
/// </summary>
[Flags]
public enum ProfilerContextInfo
{
    /// <summary>
    /// Displays no supplemental runtime context.
    /// </summary>
    None = 0,

    /// <summary>
    /// Displays scene layer, grid-cell, and active tile-animation counts.
    /// </summary>
    Scene = 1 << 0,

    /// <summary>
    /// Displays camera position, viewport zoom, and viewport dimensions.
    /// </summary>
    View = 1 << 1,

    /// <summary>
    /// Displays logical backbuffer dimensions.
    /// </summary>
    Backbuffer = 1 << 2,

    /// <summary>
    /// Displays target FPS and VSync configuration.
    /// </summary>
    EngineConfiguration = 1 << 3,

    /// <summary>
    /// Displays requested, actual, and maximum MSAA values when the backbuffer is GPU-backed.
    /// </summary>
    Msaa = 1 << 4,

    /// <summary>
    /// Displays every available supplemental runtime-context section.
    /// </summary>
    All = Scene | View | Backbuffer | EngineConfiguration | Msaa
}

/// <summary>
/// Provides detached telemetry and rendering context to a
/// <see cref="ProfilerWidget.AdditionalLinesProvider"/> callback.
/// </summary>
public sealed class ProfilerWidgetExtensionContext
{
    /// <summary>
    /// Initializes a new profiler-widget extension context.
    /// </summary>
    /// <param name="snapshot">The latest detached telemetry snapshot, or <see langword="null"/> before the first window completes.</param>
    /// <param name="renderSurfaceHost">The render surface host displayed by the widget.</param>
    /// <param name="view">The view displayed by the widget.</param>
    public ProfilerWidgetExtensionContext(
        TelemetrySnapshot? snapshot,
        RenderSurfaceHostBase renderSurfaceHost,
        View view)
    {
        Snapshot = snapshot;
        RenderSurfaceHost = renderSurfaceHost ?? throw new ArgumentNullException(nameof(renderSurfaceHost));
        View = view ?? throw new ArgumentNullException(nameof(view));
    }

    /// <summary>
    /// Gets the latest detached telemetry snapshot, or <see langword="null"/> before the first window completes.
    /// </summary>
    public TelemetrySnapshot? Snapshot { get; }

    /// <summary>
    /// Gets the render surface host displayed by the widget.
    /// </summary>
    public RenderSurfaceHostBase RenderSurfaceHost { get; }

    /// <summary>
    /// Gets the view displayed by the widget.
    /// </summary>
    public View View { get; }
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
    private const int MetricSelectorHeight = 30;
    private const int MetricSelectorWidth = 92;
    private const int MetricSelectorMargin = 4;
    private const int MaximumDirectMetricMenuItems = 18;

    private readonly Dictionary<string, bool> _sourceVisibility = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _measurementVisibility = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _sourceMeasurementVisibility = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _measurementDescriptions = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _renderedMetricLines = [];
    private readonly TooltipWidget _measurementTooltip;
    private Size _size;
    private TimeSpan _refreshInterval = TimeSpan.FromMilliseconds(250);
    private long _lastRefreshTimestamp;
    private IDisposable? _collectionRequest;
    private bool _disposed;
    private string _headerText = "Gondwana Runtime Profiler";
    private ProfilerMeasurementVisibilityMode _measurementVisibilityMode = ProfilerMeasurementVisibilityMode.All;
    private bool _measurementTooltipsEnabled = true;
    private bool _showMetricSelector = true;
    private TelemetrySnapshot? _latestSnapshot;
    private ButtonWidget? _metricSelectorButton;
    private ListBoxWidget? _metricSelectorList;
    private readonly List<MetricSelectorRow> _metricSelectorRows = [];
    private string? _metricSelectorSignature;
    private bool _metricSelectorOpen;
    private int _profilerZOrder;
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
        Rectangle displayBounds = GetDisplayBounds(bounds, showSelector: true);

        Display = new LabelWidget(
            renderSurfaceHost,
            view,
            displayBounds,
            nickname: $"{Nickname}.display")
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollEndPaddingPx = 8f
        };

        Display
            .SetFont(SKTypeface.Default, 15f, minSize: 9f)
            .SetColors(SKColors.White, new SKColor(0, 0, 0, 102))
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Top)
            .SetPadding(12f, 10f)
            .EnableWrapping(false);

        Add(
            Display,
            new Vector2(
                displayBounds.X - bounds.X,
                displayBounds.Y - bounds.Y));

        _measurementTooltip = new TooltipWidget(
            renderSurfaceHost,
            view,
            new Size(420, 78),
            $"{Nickname}.tooltip");

        IsInputEnabled = true;
        IsPointerInputEnabled = true;
        IsKeyboardInputEnabled = false;
        CanReceiveFocus = false;

        PointerMove += OnProfilerPointerMove;
        PointerLeave += OnProfilerPointerLeave;
        Display.PointerMove += OnProfilerPointerMove;
        Display.PointerLeave += OnProfilerPointerLeave;
        Display.MouseWheel += OnProfilerMouseWheel;

        Display.SetText("Gondwana Runtime Profiler\nInactive. Call Show() to collect telemetry.");
        SetIsVisible(false);
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
            ApplyLayout();
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
    public ProfilerMeasurementVisibilityMode MeasurementVisibilityMode
    {
        get => _measurementVisibilityMode;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));

            if (_measurementVisibilityMode == value)
                return;

            _measurementVisibilityMode = value;
            _metricSelectorSignature = null;
        }
    }

    /// <summary>
    /// Gets or sets the heading displayed when <see cref="ShowHeader"/> is enabled.
    /// </summary>
    public string HeaderText
    {
        get => _headerText;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _headerText = value;
        }
    }

    /// <summary>
    /// Gets or sets whether <see cref="HeaderText"/> is displayed.
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
        set
        {
            if (_showUnavailableMeasurements == value)
                return;

            _showUnavailableMeasurements = value;
            _metricSelectorSignature = null;
        }
    }

    /// <summary>
    /// Gets or sets whether hovering a rendered measurement shows its definition.
    /// </summary>
    public bool MeasurementTooltipsEnabled
    {
        get => _measurementTooltipsEnabled;
        set
        {
            if (_measurementTooltipsEnabled == value)
                return;

            _measurementTooltipsEnabled = value;
            _metricSelectorSignature = null;

            if (!value)
                _measurementTooltip.HideTooltip();
        }
    }

    /// <summary>
    /// Gets or sets whether the built-in Metrics menu is displayed.
    /// </summary>
    /// <remarks>
    /// The menu is populated from the latest detached snapshot and exposes per-source
    /// measurement check items together with show-all/hide-all, unavailable-state,
    /// and tooltip controls.
    /// </remarks>
    public bool ShowMetricSelector
    {
        get => _showMetricSelector;
        set
        {
            if (_showMetricSelector == value)
                return;

            _showMetricSelector = value;
            _metricSelectorSignature = null;

            if (!value)
                DisposeMetricSelector();

            ApplyLayout();

            if (value && _latestSnapshot is not null)
                EnsureMetricSelector(_latestSnapshot);
        }
    }

    /// <summary>
    /// Gets the tooltip used for measurement definitions.
    /// </summary>
    public TooltipWidget MeasurementTooltip => _measurementTooltip;

    /// <summary>
    /// Gets or sets the supplemental runtime context rendered before profiler measurements.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="ProfilerContextInfo.None"/> so ordinary profiler displays
    /// remain telemetry-only unless a consumer explicitly requests broader runtime context.
    /// </remarks>
    public ProfilerContextInfo ContextInfo { get; set; } = ProfilerContextInfo.None;

    /// <summary>
    /// Gets or sets an optional callback that supplies application-specific diagnostic lines.
    /// </summary>
    /// <remarks>
    /// The callback runs on the profiler widget's refresh thread, normally the Engine thread,
    /// and should remain lightweight. Returned lines are inserted after the header and before
    /// built-in runtime context and profiler measurements.
    /// </remarks>
    public Func<ProfilerWidgetExtensionContext, IEnumerable<string>?>? AdditionalLinesProvider { get; set; }

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
        _metricSelectorSignature = null;
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
        _metricSelectorSignature = null;
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
        _metricSelectorSignature = null;
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
        _metricSelectorSignature = null;
        return this;
    }

    /// <summary>
    /// Defines or replaces hover help for a measurement key.
    /// </summary>
    /// <param name="metricKey">The metric key.</param>
    /// <param name="description">Human-readable definition. Null or whitespace removes the override.</param>
    /// <returns>The current widget.</returns>
    public ProfilerWidget SetMeasurementDescription(string metricKey, string? description)
    {
        ValidateMetricKey(metricKey);

        if (string.IsNullOrWhiteSpace(description))
            _measurementDescriptions.Remove(metricKey);
        else
            _measurementDescriptions[metricKey] = description.Trim();

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
        _profilerZOrder = zOrder;
        Display.SetLabelZOrder(zOrder);
        _metricSelectorButton?.SetButtonZOrder(zOrder + 100);
        _metricSelectorList?.SetListBoxZOrder(zOrder + 200);
        _measurementTooltip.SetTooltipZOrder(zOrder + 1000);
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
        _latestSnapshot = snapshot;

        string text = FormatSnapshot(snapshot);
        CaptureRenderedMetricLines(text);
        Display.SetText(text);

        if (snapshot is not null)
            EnsureMetricSelector(snapshot);

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
        _measurementTooltip.HideTooltip();
        StopCollection();
        base.ProcessHidden();
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _measurementTooltip.HideTooltip();
        _measurementTooltip.Dispose();
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
            text.AppendLine(HeaderText);

        View? view = View;
        if (view is not null)
        {
            var extensionContext = new ProfilerWidgetExtensionContext(
                snapshot,
                RenderSurfaceHost,
                view);

            AppendAdditionalLines(text, extensionContext);
            AppendRuntimeContext(text, extensionContext);
        }

        if (snapshot is null)
        {
            text.Append("Waiting for first completed telemetry window...");
            return text.ToString().TrimEnd();
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

    private void AppendAdditionalLines(
        StringBuilder text,
        ProfilerWidgetExtensionContext context)
    {
        IEnumerable<string>? lines = AdditionalLinesProvider?.Invoke(context);
        if (lines is null)
            return;

        foreach (string line in lines)
            text.AppendLine(line ?? string.Empty);
    }

    private void AppendRuntimeContext(
        StringBuilder text,
        ProfilerWidgetExtensionContext context)
    {
        if ((ContextInfo & ProfilerContextInfo.Scene) != 0)
        {
            var scene = context.RenderSurfaceHost.Scene;
            long gridCells = scene.SceneLayers.Sum(
                static layer => (long)layer.GridColumnCount * layer.GridRowCount);

            text.Append("Animating tiles: ")
                .AppendLine(Tile.TilesAnimating.Count.ToString("N0"));
            text.Append("Layers / grid cells: ")
                .Append(scene.SceneLayers.Count)
                .Append(" / ")
                .AppendLine(gridCells.ToString("N0"));
        }

        Rectangle viewport = context.View.Viewport.TargetRectPx;
        if ((ContextInfo & ProfilerContextInfo.View) != 0)
        {
            PointF camera = context.View.Camera.PositionPx;
            text.Append("Camera: ")
                .Append(camera.X.ToString("0.0"))
                .Append(", ")
                .Append(camera.Y.ToString("0.0"))
                .Append(" px; zoom: ")
                .Append(context.View.Viewport.Zoom.ToString("0.000"))
                .AppendLine("x");
        }

        bool includeViewport = (ContextInfo & ProfilerContextInfo.View) != 0;
        bool includeBackbuffer = (ContextInfo & ProfilerContextInfo.Backbuffer) != 0;
        if (includeViewport && includeBackbuffer)
        {
            var backbuffer = context.RenderSurfaceHost.Backbuffer;
            text.Append("Viewport / backbuffer: ")
                .Append(viewport.Width)
                .Append('x')
                .Append(viewport.Height)
                .Append(" / ")
                .Append(backbuffer.Width)
                .Append('x')
                .AppendLine(backbuffer.Height.ToString());
        }
        else if (includeViewport)
        {
            text.Append("Viewport: ")
                .Append(viewport.Width)
                .Append('x')
                .AppendLine(viewport.Height.ToString());
        }
        else if (includeBackbuffer)
        {
            var backbuffer = context.RenderSurfaceHost.Backbuffer;
            text.Append("Backbuffer: ")
                .Append(backbuffer.Width)
                .Append('x')
                .AppendLine(backbuffer.Height.ToString());
        }

        if ((ContextInfo & ProfilerContextInfo.EngineConfiguration) != 0)
        {
            text.Append("Target FPS / VSync: ")
                .Append(Engine.Instance.Configuration.TargetFPS)
                .Append(" / ")
                .AppendLine(Engine.Instance.Configuration.VSync.ToString());
        }

        if ((ContextInfo & ProfilerContextInfo.Msaa) != 0 &&
            context.RenderSurfaceHost.Backbuffer is GpuBackbuffer gpu)
        {
            text.Append("MSAA requested / actual / max: ")
                .Append(gpu.MsaaSampleCount)
                .Append(" / ")
                .Append(gpu.ActualMsaaSampleCount)
                .Append(" / ")
                .AppendLine(gpu.MaxSupportedMsaaSampleCount.ToString());
        }
    }

    private void ApplyLayout()
    {
        Rectangle bounds = Bounds;
        Rectangle displayBounds = GetDisplayBounds(bounds, ShowMetricSelector);

        SetLocalOffset(
            Display,
            new Vector2(
                displayBounds.X - bounds.X,
                displayBounds.Y - bounds.Y));
        Display.Size = displayBounds.Size;

        if (_metricSelectorButton is not null || _metricSelectorList is not null)
        {
            DisposeMetricSelector();
            _metricSelectorSignature = null;

            if (ShowMetricSelector && _latestSnapshot is not null)
                EnsureMetricSelector(_latestSnapshot);
        }
    }

    private static Rectangle GetDisplayBounds(Rectangle bounds, bool showSelector)
    {
        int toolbarHeight = showSelector
            ? Math.Min(MetricSelectorHeight, Math.Max(0, bounds.Height - 1))
            : 0;

        return new Rectangle(
            bounds.Left,
            bounds.Top + toolbarHeight,
            bounds.Width,
            Math.Max(1, bounds.Height - toolbarHeight));
    }

    private static Rectangle GetMetricSelectorButtonBounds(Rectangle bounds)
    {
        int width = Math.Max(
            1,
            Math.Min(
                MetricSelectorWidth,
                Math.Max(1, bounds.Width - MetricSelectorMargin * 2)));
        int height = Math.Max(
            1,
            Math.Min(
                MetricSelectorHeight - MetricSelectorMargin * 2,
                Math.Max(1, bounds.Height)));

        return new Rectangle(
            bounds.Left + MetricSelectorMargin,
            bounds.Top + MetricSelectorMargin,
            width,
            height);
    }

    private static Rectangle GetMetricSelectorListBounds(Rectangle bounds)
    {
        int width = Math.Max(24, Math.Min(460, Math.Max(24, bounds.Width - MetricSelectorMargin * 2)));
        int availableHeight = Math.Max(
            28,
            bounds.Height - MetricSelectorHeight - MetricSelectorMargin);
        int height = Math.Max(28, Math.Min(420, availableHeight));

        return new Rectangle(
            bounds.Left + MetricSelectorMargin,
            bounds.Top + MetricSelectorHeight,
            width,
            height);
    }

    private void EnsureMetricSelector(TelemetrySnapshot snapshot)
    {
        if (!ShowMetricSelector || View is null)
            return;

        string signature = BuildMetricSelectorSignature(snapshot);
        if (_metricSelectorButton is null ||
            _metricSelectorList is null ||
            !string.Equals(_metricSelectorSignature, signature, StringComparison.Ordinal))
        {
            bool wasOpen = _metricSelectorOpen;
            DisposeMetricSelector();
            _metricSelectorOpen = wasOpen;

            Rectangle bounds = Bounds;
            Rectangle buttonBounds = GetMetricSelectorButtonBounds(bounds);
            Rectangle listBounds = GetMetricSelectorListBounds(bounds);

            _metricSelectorButton = new ButtonWidget(
                    RenderSurfaceHost,
                    View,
                    buttonBounds,
                    "Metrics...",
                    $"{Nickname}.metric-selector.button")
                .SetButtonZOrder(_profilerZOrder + 100);

            _metricSelectorButton.Clicked += ToggleMetricSelector;

            _metricSelectorList = new ListBoxWidget(
                RenderSurfaceHost,
                View,
                listBounds,
                nickname: $"{Nickname}.metric-selector.list")
            {
                ItemHeight = 22,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            _metricSelectorList
                .SetListBoxZOrder(_profilerZOrder + 200)
                .SetSelectionColor(Color.FromArgb(255, 70, 70, 84));

            _metricSelectorList.SelectionCommitted += OnMetricSelectorSelectionCommitted;

            Add(
                _metricSelectorButton,
                new Vector2(
                    buttonBounds.X - bounds.X,
                    buttonBounds.Y - bounds.Y));
            Add(
                _metricSelectorList,
                new Vector2(
                    listBounds.X - bounds.X,
                    listBounds.Y - bounds.Y));

            _metricSelectorSignature = signature;

            if (!_metricSelectorOpen)
                _metricSelectorList.Hide();
        }

        RefreshMetricSelectorRows(snapshot);
    }

    private string BuildMetricSelectorSignature(TelemetrySnapshot snapshot)
    {
        var signature = new StringBuilder()
            .Append(Bounds.Width)
            .Append('x')
            .Append(Bounds.Height);

        foreach (var sourceGroup in snapshot.Sources
                     .GroupBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            signature.Append('|').Append(sourceGroup.Key);

            foreach (string backend in sourceGroup
                         .Select(source => source.Backend)
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                signature.Append('[').Append(backend).Append(']');
            }

            foreach (string metricKey in sourceGroup
                         .SelectMany(source => source.Metrics.Keys)
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                signature.Append(';').Append(metricKey);
            }
        }

        return signature.ToString();
    }

    private void RefreshMetricSelectorRows(TelemetrySnapshot snapshot)
    {
        if (_metricSelectorList is null)
            return;

        _metricSelectorRows.Clear();

        AddSelectorRow(
            MeasurementTooltipsEnabled,
            "Hover definitions",
            () => MeasurementTooltipsEnabled = !MeasurementTooltipsEnabled);
        AddSelectorRow(
            ShowUnavailableMeasurements,
            "Show unavailable",
            () => ShowUnavailableMeasurements = !ShowUnavailableMeasurements);
        _metricSelectorRows.Add(new MetricSelectorRow("Show all measurements", () =>
        {
            MeasurementVisibilityMode = ProfilerMeasurementVisibilityMode.All;
            ClearVisibilityOverrides();
        }));
        _metricSelectorRows.Add(new MetricSelectorRow("Hide all measurements", () =>
        {
            MeasurementVisibilityMode = ProfilerMeasurementVisibilityMode.Selected;
            ClearVisibilityOverrides();
        }));
        _metricSelectorRows.Add(new MetricSelectorRow("------------------------------", null));

        foreach (var sourceGroup in snapshot.Sources
                     .GroupBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            string sourceName = sourceGroup.Key;
            string backend = string.Join(
                "/",
                sourceGroup
                    .Select(source => source.Backend)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal));

            bool sourceVisible = IsSourceVisible(sourceName);
            AddSelectorRow(
                sourceVisible,
                $"{sourceName} [{backend}]",
                () => SetSourceVisible(sourceName, !IsSourceVisible(sourceName)));

            foreach (string metricKey in sourceGroup
                         .SelectMany(source => source.Metrics.Keys)
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                bool metricVisible = IsMeasurementVisible(sourceName, metricKey);
                string capturedMetricKey = metricKey;
                string capturedSourceName = sourceName;

                AddSelectorRow(
                    metricVisible,
                    $"  {metricKey}",
                    () => SetMeasurementVisible(
                        capturedSourceName,
                        capturedMetricKey,
                        !IsMeasurementVisible(capturedSourceName, capturedMetricKey)));
            }
        }

        int topIndex = _metricSelectorList.TopIndex;
        _metricSelectorList.SetItems(_metricSelectorRows.Select(row => row.Text));

        int maximumTopIndex = Math.Max(
            0,
            _metricSelectorRows.Count - _metricSelectorList.VisibleItemCount);
        _metricSelectorList.TopIndex = Math.Min(topIndex, maximumTopIndex);
    }

    private void AddSelectorRow(bool isChecked, string text, Action action)
    {
        _metricSelectorRows.Add(
            new MetricSelectorRow(
                $"[{(isChecked ? 'x' : ' ')}] {text}",
                action));
    }

    private void ToggleMetricSelector()
    {
        if (_metricSelectorList is null)
            return;

        _metricSelectorOpen = !_metricSelectorOpen;
        if (_metricSelectorOpen)
        {
            _metricSelectorList.Show();
            _metricSelectorList.Activate();
        }
        else
        {
            _metricSelectorList.Hide();
        }
    }

    private void OnMetricSelectorSelectionCommitted(int index)
    {
        if (index < 0 || index >= _metricSelectorRows.Count)
            return;

        MetricSelectorRow row = _metricSelectorRows[index];
        if (row.Action is null)
            return;

        row.Action();
        Refresh();
    }

    private void DisposeMetricSelector()
    {
        if (_metricSelectorButton is not null)
        {
            _metricSelectorButton.Clicked -= ToggleMetricSelector;
            RemoveChild(_metricSelectorButton, dispose: true);
            _metricSelectorButton = null;
        }

        if (_metricSelectorList is not null)
        {
            _metricSelectorList.SelectionCommitted -= OnMetricSelectorSelectionCommitted;
            RemoveChild(_metricSelectorList, dispose: true);
            _metricSelectorList = null;
        }

        _metricSelectorRows.Clear();
        _metricSelectorSignature = null;
    }

    private void CaptureRenderedMetricLines(string text)
    {
        _renderedMetricLines.Clear();

        string[] lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (!line.StartsWith("  ", StringComparison.Ordinal))
                continue;

            int colon = line.IndexOf(':', 2);
            if (colon <= 2)
                continue;

            string metricKey = line[2..colon].Trim();
            if (!string.IsNullOrWhiteSpace(metricKey))
                _renderedMetricLines[index] = metricKey;
        }
    }

    private void OnProfilerPointerMove(WidgetPointerEventArgs args)
    {
        if (!MeasurementTooltipsEnabled || !Visible)
        {
            _measurementTooltip.HideTooltip();
            return;
        }

        Rectangle displayBounds = Display.Bounds;
        Point pointer = Point.Round(args.ScreenPositionPx);
        if (!displayBounds.Contains(pointer))
        {
            _measurementTooltip.HideTooltip();
            return;
        }

        float lineHeight = Display.TextBlock.LineHeight;
        if (lineHeight <= 0f)
        {
            _measurementTooltip.HideTooltip();
            return;
        }

        float localY =
            args.ScreenPositionPx.Y -
            displayBounds.Top -
            Display.TextBlock.VerticalPadding +
            Display.VerticalScrollOffsetPx;

        if (localY < 0f)
        {
            _measurementTooltip.HideTooltip();
            return;
        }

        int lineIndex = (int)MathF.Floor(localY / lineHeight);
        if (!_renderedMetricLines.TryGetValue(lineIndex, out string? metricKey))
        {
            _measurementTooltip.HideTooltip();
            return;
        }

        string? description = GetMeasurementDescription(metricKey);
        if (string.IsNullOrWhiteSpace(description))
        {
            _measurementTooltip.HideTooltip();
            return;
        }

        _measurementTooltip.ShowTooltip(
            $"{metricKey}\n{description}",
            pointer);
    }

    private void OnProfilerPointerLeave(WidgetPointerEventArgs args)
    {
        _ = args;
        _measurementTooltip.HideTooltip();
    }

    private void OnProfilerMouseWheel(WidgetMouseWheelEventArgs args)
    {
        _ = args;
        _measurementTooltip.HideTooltip();
    }

    private string? GetMeasurementDescription(string metricKey)
    {
        if (_measurementDescriptions.TryGetValue(metricKey, out string? description))
            return description;

        string normalizedKey = NormalizeLayerMetricKey(metricKey);
        return normalizedKey switch
        {
            "cycle.cpu.ms" =>
                "CPU time for one complete Engine cycle. The sample rate is the Engine cycle rate (CPS).",
            "background.cpu.ms" =>
                "CPU time spent in background/simulation work for one Engine cycle.",
            "foreground.cpu.ms" =>
                "CPU time spent in foreground/render-preparation work. The sample rate is foreground frame production.",
            "build.cpu.ms" =>
                "Engine-thread time spent building the render frame or render snapshot.",
            "query.cpu.ms" =>
                "Time spent querying and culling visible drawables. Sorting is reported separately.",
            "sort.cpu.ms" =>
                "Time spent sorting queried drawables into render order.",
            "record.cpu.ms" =>
                "Time spent recording render commands after query/sort.",
            "overlay.cpu.ms" =>
                "Time spent recording view/direct-drawing overlay work.",
            "visible.drawables" =>
                "Visible drawable instances observed during a render build, including repeated instances across views.",
            "visible.tiles" =>
                "Visible tile draw instances observed during a render build.",
            "atlas.batches" =>
                "Atlas-backed tile batches generated for a render build.",
            "atlas.tiles" =>
                "Tiles emitted through atlas-backed batches for a render build.",
            "replay.cpu.ms" =>
                "GPU presentation-thread time spent replaying a completed render snapshot.",
            "picture.cpu.ms" =>
                "Time spent replaying the recorded Skia picture into the GPU backbuffer.",
            "backbuffer.flush.cpu.ms" =>
                "Time spent flushing GPU backbuffer drawing after replay.",
            "snapshot.cpu.ms" =>
                "Time spent snapshotting the current GPU backbuffer for presentation/inspection.",
            "snapshot.age.ms" =>
                "Age of the consumed render snapshot when GPU replay begins. Higher values indicate producer/consumer lag.",
            "mailbox.published.lifetime" =>
                "Lifetime count of render snapshots published to the producer/consumer mailbox.",
            "mailbox.dropped.lifetime" =>
                "Lifetime count of published snapshots superseded before they could be consumed.",
            "mailbox.slots" =>
                "Current occupied snapshot-mailbox slots.",
            "snapshot.commands.approximate" =>
                "Approximate command count retained by the latest render snapshot.",
            "gate.wait.cpu.ms" =>
                "Desktop GPU time spent waiting to enter the render/presentation gate.",
            "gate.held.cpu.ms" =>
                "Desktop GPU time spent while holding the render/presentation gate.",
            "presentation.count" =>
                "Completed presentation callbacks. Its sample rate is the actual GPU presentation FPS for this render surface.",
            "presentation.cpu.ms" =>
                "Total CPU duration of one GPU presentation callback.",
            "render.snapshot.cpu.ms" =>
                "Combined render/snapshot CPU work measured by the GPU presentation path.",
            "blit.cpu.ms" =>
                "Platform presentation time spent blitting the completed image.",
            "flush.cpu.ms" =>
                "Platform presentation time spent performing the final graphics flush.",
            "layers.omitted" =>
                "Number of SceneLayers omitted from detailed per-layer telemetry because the retained layer-detail limit was reached.",
            "layer.query.cpu.ms" =>
                "Per-layer time spent querying/culling visible content; sorting is excluded.",
            "layer.record.cpu.ms" =>
                "Per-layer time spent recording render commands.",
            "layer.drawables" =>
                "Visible drawable instances contributed by this SceneLayer across views.",
            "layer.tiles" =>
                "Visible tile draw instances contributed by this SceneLayer.",
            "layer.transformed.tiles" =>
                "Current count of this layer's visible tiles requiring transform work.",
            "layer.tile.width.px" =>
                "Current native tile width for this SceneLayer.",
            "layer.tile.height.px" =>
                "Current native tile height for this SceneLayer.",
            "layer.z" =>
                "Current SceneLayer Z-order.",
            _ => null
        };
    }

    private static string NormalizeLayerMetricKey(string metricKey)
    {
        if (!metricKey.StartsWith("layer.", StringComparison.Ordinal))
            return metricKey;

        int firstSeparator = metricKey.IndexOf('.', "layer.".Length);
        if (firstSeparator < 0 || firstSeparator + 1 >= metricKey.Length)
            return metricKey;

        return "layer." + metricKey[(firstSeparator + 1)..];
    }

    private sealed record MetricSelectorRow(string Text, Action? Action);

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
