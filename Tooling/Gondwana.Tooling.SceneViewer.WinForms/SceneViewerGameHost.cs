using System.Text;
using Gondwana.Configuration;
using Gondwana.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Timers;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using SkiaSharp;
using GondwanaMouseEventArgs = Gondwana.Input.Mouse.MouseEventArgs;
using GondwanaView = Gondwana.Rendering.Views.View;

namespace Gondwana.Tooling.SceneViewer.WinForms;

internal sealed class SceneViewerGameHost(WinFormGpuRenderSurfaceControl surface, string scenePath)
    : WinFormsGpuGameHost(surface)
{
    private const int DiagnosticsMargin = 12;
    private const int DiagnosticsWidth = 700;
    private const int DiagnosticsHeight = 560;
    private const int MaxDiagnosticLayers = 8;

    private readonly HashSet<Keys> _keysDown = [];
    private readonly object _renderDiagnosticsLock = new();
    private readonly Dictionary<int, LayerDiagnosticsWindow> _layerDiagnostics = [];

    private long _lastTick;
    private long _backgroundStartTick;
    private long _backgroundTotalTicks;
    private long _backgroundMaxTicks;
    private long _backgroundSampleCount;
    private int _animatingTileCount;

    private long _sceneRenderSamples;
    private double _sceneRenderTotalMs;
    private double _sceneRenderMaxMs;
    private double _queryAndSortTotalMs;
    private double _drawTotalMs;
    private double _overlayTotalMs;
    private long _visibleDrawableTotal;
    private long _visibleTileTotal;

    private long _gpuCallbackSamples;
    private double _gpuCallbackTotalMs;
    private double _gpuCallbackMaxMs;
    private double _renderAndSnapshotTotalMs;
    private double _blitTotalMs;
    private double _flushTotalMs;

    private long _gpuSynchronizationSamples;
    private double _replayTotalMs, _replayMaxMs, _ageTotalMs;
    private long _publishedSnapshots, _droppedSnapshots;
    private int _snapshotSlotsInUse;
    private double _gpuLockWaitTotalMs;
    private double _gpuLockWaitMaxMs;
    private double _gpuLockHeldTotalMs;
    private double _gpuLockHeldMaxMs;

    private bool _animationsPaused;
    private GondwanaView? _view;
    private TextBlock? _diagnosticsText;
    private Gondwana.CyclesPerSecondCalculatedEventArgs? _lastCpsSample;

    internal ViewerCameraController? Camera { get; private set; }

    internal event Action? CloseRequested;

    protected override Scene CreateInitialScene() => new ViewerSceneLoader().Load(scenePath);

    protected override void OnSceneBound()
    {
        RenderSurface.Host.ViewManager.ConfigureSingleFullView();
        _view = RenderSurface.Host.ViewManager.Views[0];
        _view.Camera.WorldBoundsPx = RectangleF.Empty;
        _view.Camera.SnapTo(PointF.Empty);

        var zoomLayer = Scene!.SceneLayers
            .Where(layer => layer.Visible && Math.Abs(layer.Parallax) > 1e-6f)
            .OrderBy(layer => Math.Abs(layer.Parallax - 1f))
            .ThenByDescending(layer => layer.ZOrder)
            .FirstOrDefault()
            ?? Scene.SceneLayers.FirstOrDefault();

        Camera = new ViewerCameraController(_view, zoomLayer);
    }

    protected override void CreateDirectDrawings()
    {
        if (_view is null)
            return;

        _diagnosticsText = new TextBlock(
                RenderSurface.Host,
                _view,
                GetDiagnosticsBounds(_view.Viewport.TargetRectPx),
                "scene-viewer-diagnostics")
            .SetFont(SKTypeface.Default, 15f)
            .SetColors(SKColors.White, SKColors.Black)
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Top)
            .EnableWrapping(false);

        _diagnosticsText.HorizontalPadding = 12f;
        _diagnosticsText.VerticalPadding = 10f;
        _diagnosticsText.LineSpacingMultiplier = 1.05f;
        _diagnosticsText.ZOrder = 20_000;
        _diagnosticsText.Visible = false;

        _view.Viewport.TargetRectChanged += OnViewportTargetRectChanged;
    }

    protected override void OnKeyboardAdapterInitialized()
    {
        var keyboard = Engine.Input.KeyboardEventPoller!;
        keyboard.KeyDown += OnKeyDown;

        foreach (var key in MonitoredKeys)
            keyboard.StartMonitoringKey((int)key, key.ToString());
    }

    protected override void OnMouseAdapterInitialized()
    {
        var mouse = Engine.Input.MouseEventPoller!;
        mouse.MouseEvent += OnMouse;
        mouse.StartMonitoringMouse(
            trackMouseMovement: false,
            timeBetweenEvents: 0);
    }

    protected override void OnEngineInitialized()
    {
        _lastTick = HighResTimer.GetCurrentTick();
        Engine.BeforeBackgroundTasksExecute += BeforeBackgroundTasksExecute;
        Engine.AfterBackgroundTasksExecute += AfterBackgroundTasksExecute;
        Engine.CPSCalculated += OnCpsCalculated;
    }

    protected override void ConfigureGamepads() { }

    protected override void UnhookEvents()
    {
        if (Engine.Input.KeyboardEventPoller is not null)
            Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;

        if (Engine.Input.MouseEventPoller is not null)
            Engine.Input.MouseEventPoller.MouseEvent -= OnMouse;

        Engine.BeforeBackgroundTasksExecute -= BeforeBackgroundTasksExecute;
        Engine.AfterBackgroundTasksExecute -= AfterBackgroundTasksExecute;
        Engine.CPSCalculated -= OnCpsCalculated;
        RenderSurface.Host.GpuRenderFrameDiagnosticsCalculated -= OnGpuRenderFrameDiagnostics;
        RenderSurface.Host.GpuRenderSynchronizationDiagnosticsCalculated -= OnGpuRenderSynchronizationDiagnostics;
        RenderSurface.Adapter.FrameDiagnosticsCalculated -= OnGpuFrameDiagnostics;

        if (_view is not null)
            _view.Viewport.TargetRectChanged -= OnViewportTargetRectChanged;
    }

    private static Keys[] MonitoredKeys =>
    [
        Keys.W,
        Keys.A,
        Keys.S,
        Keys.D,
        Keys.Up,
        Keys.Down,
        Keys.Left,
        Keys.Right,
        Keys.Home,
        Keys.Escape,
        Keys.F3,
        Keys.F4
    ];

    private void OnKeyDown(KeyDownEventArgs args)
    {
        if (!Enum.TryParse(args.KeyConfig.Key, true, out Keys key))
            return;

        if (args.KeyAction == KeyAction.Pressed)
        {
            if (key == Keys.F3)
            {
                ToggleDiagnostics();
                return;
            }

            if (key == Keys.F4)
            {
                ToggleAnimations();
                return;
            }

            _keysDown.Add(key);

            if (key == Keys.Home)
                Camera?.Reset();
            else if (key == Keys.Escape)
                CloseRequested?.Invoke();
        }
        else if (args.KeyAction == KeyAction.Released)
        {
            _keysDown.Remove(key);
        }
    }

    private void OnMouse(GondwanaMouseEventArgs args)
    {
        if (args.ScrollDelta != 0)
            Camera?.Zoom(args.CurrentPosition, args.ScrollDelta);
    }

    private void BeforeBackgroundTasksExecute()
    {
        _backgroundStartTick = HighResTimer.GetCurrentTick();
        UpdateCamera();
    }

    private void AfterBackgroundTasksExecute()
    {
        long elapsedTicks = Math.Max(0, HighResTimer.GetCurrentTick() - _backgroundStartTick);
        Interlocked.Add(ref _backgroundTotalTicks, elapsedTicks);
        Interlocked.Increment(ref _backgroundSampleCount);
        RecordMaximum(ref _backgroundMaxTicks, elapsedTicks);
        Volatile.Write(ref _animatingTileCount, Tile.TilesAnimating.Count);
    }

    private void UpdateCamera()
    {
        long tick = HighResTimer.GetCurrentTick();
        double elapsed = Math.Clamp(HighResTimer.GetDuration(_lastTick, tick), 0f, .05f);
        _lastTick = tick;

        if (elapsed <= 0 || Camera is not { } camera)
            return;

        bool north = _keysDown.Contains(Keys.W) || _keysDown.Contains(Keys.Up);
        bool south = _keysDown.Contains(Keys.S) || _keysDown.Contains(Keys.Down);
        bool west = _keysDown.Contains(Keys.A) || _keysDown.Contains(Keys.Left);
        bool east = _keysDown.Contains(Keys.D) || _keysDown.Contains(Keys.Right);

        var modifiers = Engine.Input.KeyboardEventPoller?.Adapter?.CurrentKeyboardModifiers
            ?? KeyboardModifierState.None;
        bool fast = (modifiers & KeyboardModifierState.Shift) != 0;

        camera.Move(north, south, west, east, fast, elapsed);
    }

    private void ToggleAnimations()
    {
        _animationsPaused = !_animationsPaused;

        foreach (Tile tile in Tile.TilesAnimating.ToArray())
            tile.PauseAnimation = _animationsPaused;

        if (_diagnosticsText?.Visible == true)
            UpdateDiagnosticsText(_lastCpsSample);
    }

    private void OnGpuRenderFrameDiagnostics(GpuRenderFrameDiagnostics diagnostics)
    {
        lock (_renderDiagnosticsLock)
        {
            _sceneRenderSamples++;
            _sceneRenderTotalMs += diagnostics.TotalRenderMilliseconds;
            _sceneRenderMaxMs = Math.Max(_sceneRenderMaxMs, diagnostics.TotalRenderMilliseconds);
            _queryAndSortTotalMs += diagnostics.QueryAndSortMilliseconds;
            _drawTotalMs += diagnostics.DrawMilliseconds;
            _overlayTotalMs += diagnostics.OverlayMilliseconds;
            _visibleDrawableTotal += diagnostics.DrawableCount;
            _visibleTileTotal += diagnostics.TileCount;

            foreach (var layer in diagnostics.Layers)
            {
                if (!_layerDiagnostics.TryGetValue(layer.LayerIndex, out var window))
                {
                    window = new LayerDiagnosticsWindow(layer.LayerIndex, layer.LayerId, layer.ZOrder);
                    _layerDiagnostics.Add(layer.LayerIndex, window);
                }

                window.Add(layer);
            }
        }
    }

    private void OnGpuRenderSynchronizationDiagnostics(
        GpuRenderSynchronizationDiagnostics diagnostics)
    {
        lock (_renderDiagnosticsLock)
        {
            _gpuSynchronizationSamples++;
            _replayTotalMs += diagnostics.ReplayMilliseconds;
            _replayMaxMs = Math.Max(_replayMaxMs, diagnostics.ReplayMilliseconds);
            _ageTotalMs += diagnostics.SnapshotAgeMilliseconds;
            _publishedSnapshots = diagnostics.PublishedSnapshots;
            _droppedSnapshots = diagnostics.DroppedSnapshots;
            _snapshotSlotsInUse = diagnostics.SnapshotSlotsInUse;
            _gpuLockWaitTotalMs += diagnostics.LockWaitMilliseconds;
            _gpuLockWaitMaxMs = Math.Max(
                _gpuLockWaitMaxMs,
                diagnostics.LockWaitMilliseconds);
            _gpuLockHeldTotalMs += diagnostics.LockHeldMilliseconds;
            _gpuLockHeldMaxMs = Math.Max(
                _gpuLockHeldMaxMs,
                diagnostics.LockHeldMilliseconds);
        }
    }

    private void OnGpuFrameDiagnostics(WinFormGpuFrameDiagnostics diagnostics)
    {
        lock (_renderDiagnosticsLock)
        {
            _gpuCallbackSamples++;
            _gpuCallbackTotalMs += diagnostics.TotalCallbackMilliseconds;
            _gpuCallbackMaxMs = Math.Max(_gpuCallbackMaxMs, diagnostics.TotalCallbackMilliseconds);
            _renderAndSnapshotTotalMs += diagnostics.RenderAndSnapshotMilliseconds;
            _blitTotalMs += diagnostics.BlitMilliseconds;
            _flushTotalMs += diagnostics.FlushMilliseconds;
        }
    }

    private void OnCpsCalculated(Gondwana.CyclesPerSecondCalculatedEventArgs sample)
    {
        if (Engine.IsDisposed)
            return;

        Engine.EngineDispatcher.Post(() =>
        {
            _lastCpsSample = sample;
            if (_diagnosticsText?.Visible == true)
                UpdateDiagnosticsText(sample);
            else
            {
                ResetBackgroundDiagnosticsWindow();
                ResetRenderDiagnosticsWindow();
            }
        });
    }

    private void ToggleDiagnostics()
    {
        if (_diagnosticsText is null)
            return;

        _diagnosticsText.Visible = !_diagnosticsText.Visible;
        if (_diagnosticsText.Visible)
        {
            ResetRenderDiagnosticsWindow();
            RenderSurface.Host.GpuRenderFrameDiagnosticsCalculated += OnGpuRenderFrameDiagnostics;
            RenderSurface.Host.GpuRenderSynchronizationDiagnosticsCalculated += OnGpuRenderSynchronizationDiagnostics;
            RenderSurface.Adapter.FrameDiagnosticsCalculated += OnGpuFrameDiagnostics;
        }
        else
        {
            RenderSurface.Host.GpuRenderFrameDiagnosticsCalculated -= OnGpuRenderFrameDiagnostics;
            RenderSurface.Host.GpuRenderSynchronizationDiagnosticsCalculated -= OnGpuRenderSynchronizationDiagnostics;
            RenderSurface.Adapter.FrameDiagnosticsCalculated -= OnGpuFrameDiagnostics;
        }

        if (_diagnosticsText.Visible)
            UpdateDiagnosticsText(_lastCpsSample);
    }

    private void UpdateDiagnosticsText(Gondwana.CyclesPerSecondCalculatedEventArgs? sample)
    {
        if (_diagnosticsText is null || _view is null)
            return;

        (double averageBackgroundMs, double maxBackgroundMs, long backgroundSamples) =
            ResetBackgroundDiagnosticsWindow();
        var render = ResetRenderDiagnosticsWindow();

        var viewport = _view.Viewport.TargetRectPx;
        var cameraPosition = _view.Camera.PositionPx;
        var backbuffer = RenderSurface.Host.Backbuffer;
        int layerCount = Scene?.SceneLayers.Count ?? 0;
        long gridTileCount = Scene?.SceneLayers.Sum(
            layer => (long)layer.GridColumnCount * layer.GridRowCount) ?? 0L;

        string gpuFps = sample?.GpuFps is double gpu
            ? gpu.ToString("0.0")
            : "n/a";

        string msaa = backbuffer is GpuBackbuffer gpuBackbuffer
            ? $"{gpuBackbuffer.MsaaSampleCount} / {gpuBackbuffer.ActualMsaaSampleCount} / {gpuBackbuffer.MaxSupportedMsaaSampleCount}"
            : "n/a";

        double sceneRenderAverageMs = Average(render.SceneRenderTotalMs, render.SceneRenderSamples);
        double renderAndSnapshotAverageMs = Average(render.RenderAndSnapshotTotalMs, render.GpuCallbackSamples);
        double lockWaitAverageMs = Average(
            render.GpuLockWaitTotalMs,
            render.GpuSynchronizationSamples);
        double lockHeldAverageMs = Average(
            render.GpuLockHeldTotalMs,
            render.GpuSynchronizationSamples);
        var text = new StringBuilder()
            .AppendLine("Gondwana Scene Viewer Diagnostics  [F3]")
            .AppendLine($"Scene: {Path.GetFileName(scenePath)}")
            .AppendLine($"Animations: {(_animationsPaused ? "PAUSED" : "running")}  [F4]")
            .AppendLine($"CPS: {(sample?.GrossCPS ?? 0):0.0}")
            .AppendLine($"Engine FPS: {(sample?.NetCPS ?? 0):0.0}")
            .AppendLine($"GPU FPS: {gpuFps}")
            .AppendLine($"Background avg/max: {averageBackgroundMs:0.000} / {maxBackgroundMs:0.000} ms  ({backgroundSamples:N0} samples)")
            .AppendLine($"GL callback avg/max: {Average(render.GpuCallbackTotalMs, render.GpuCallbackSamples):0.000} / {render.GpuCallbackMaxMs:0.000} ms")
            .AppendLine($"Render+snapshot avg: {renderAndSnapshotAverageMs:0.000} ms")
            .AppendLine($"GL lock wait avg/max: {lockWaitAverageMs:0.000} / {render.GpuLockWaitMaxMs:0.000} ms")
            .AppendLine($"GL lock held avg/max: {lockHeldAverageMs:0.000} / {render.GpuLockHeldMaxMs:0.000} ms")
            .AppendLine($"Snapshot build avg/max: {sceneRenderAverageMs:0.000} / {render.SceneRenderMaxMs:0.000} ms")
            .AppendLine($"Build query/sort avg: {Average(render.QueryAndSortTotalMs, render.SceneRenderSamples):0.000} ms")
            .AppendLine($"Command record avg: {Average(render.DrawTotalMs, render.SceneRenderSamples):0.000} ms")
            .AppendLine($"Overlay record avg: {Average(render.OverlayTotalMs, render.SceneRenderSamples):0.000} ms")
            .AppendLine($"GL replay avg/max: {Average(render.ReplayTotalMs, render.GpuSynchronizationSamples):0.000} / {render.ReplayMaxMs:0.000} ms")
            .AppendLine($"Snapshot age avg: {Average(render.AgeTotalMs, render.GpuSynchronizationSamples):0.000} ms")
            .AppendLine($"Published / dropped / slots: {render.PublishedSnapshots:N0} / {render.DroppedSnapshots:N0} / {render.SnapshotSlotsInUse}/3")
            .AppendLine($"Blit avg: {Average(render.BlitTotalMs, render.GpuCallbackSamples):0.000} ms")
            .AppendLine($"Flush avg: {Average(render.FlushTotalMs, render.GpuCallbackSamples):0.000} ms")
            .AppendLine($"Visible drawables/tiles avg: {Average(render.VisibleDrawableTotal, render.SceneRenderSamples):0.0} / {Average(render.VisibleTileTotal, render.SceneRenderSamples):0.0}")
            .AppendLine($"Animating tiles: {Volatile.Read(ref _animatingTileCount):N0}")
            .AppendLine($"Layers / grid cells: {layerCount:N0} / {gridTileCount:N0}")
            .AppendLine($"Camera: {cameraPosition.X:0.0}, {cameraPosition.Y:0.0} px")
            .AppendLine($"Zoom: {_view.Viewport.Zoom:0.000}x")
            .AppendLine($"Viewport / backbuffer: {viewport.Width}x{viewport.Height} / {backbuffer.Width}x{backbuffer.Height}")
            .AppendLine($"Target FPS / VSync: {Engine.Configuration.TargetFPS} / {(Engine.Configuration.VSync ? "on" : "off")}")
            .AppendLine($"MSAA requested / actual / max: {msaa}");

        if (render.Layers.Count > 0)
        {
            text.AppendLine("Layers (avg query / draw ms; drawables / tiles):");
            foreach (var layer in render.Layers.Take(MaxDiagnosticLayers))
            {
                text.AppendLine(
                    $"  L{layer.LayerIndex} z{layer.ZOrder} {layer.TileWidth}x{layer.TileHeight} " +
                    $"xform={layer.TransformedTileCount}: " +
                    $"{Average(layer.QueryTotalMs, layer.Samples):0.000} / " +
                    $"{Average(layer.DrawTotalMs, layer.Samples):0.000}; " +
                    $"{Average(layer.DrawableTotal, layer.Samples):0.0} / " +
                    $"{Average(layer.TileTotal, layer.Samples):0.0}");
            }

            if (render.Layers.Count > MaxDiagnosticLayers)
                text.AppendLine($"  ... {render.Layers.Count - MaxDiagnosticLayers} more layer(s)");
        }

        _diagnosticsText.SetText(text.ToString().TrimEnd());
    }

    private (double AverageMs, double MaxMs, long Samples) ResetBackgroundDiagnosticsWindow()
    {
        long totalTicks = Interlocked.Exchange(ref _backgroundTotalTicks, 0);
        long maxTicks = Interlocked.Exchange(ref _backgroundMaxTicks, 0);
        long samples = Interlocked.Exchange(ref _backgroundSampleCount, 0);

        if (samples <= 0)
            return (0, 0, 0);

        double millisecondsPerTick = 1000d / HighResTimer.TicksPerSecond;
        return (
            totalTicks * millisecondsPerTick / samples,
            maxTicks * millisecondsPerTick,
            samples);
    }

    private RenderDiagnosticsSnapshot ResetRenderDiagnosticsWindow()
    {
        lock (_renderDiagnosticsLock)
        {
            var layers = _layerDiagnostics.Values
                .OrderBy(layer => layer.LayerIndex)
                .Select(layer => layer.ToSnapshot())
                .ToArray();

            var snapshot = new RenderDiagnosticsSnapshot(
                _sceneRenderSamples,
                _sceneRenderTotalMs,
                _sceneRenderMaxMs,
                _queryAndSortTotalMs,
                _drawTotalMs,
                _overlayTotalMs,
                _visibleDrawableTotal,
                _visibleTileTotal,
                _gpuCallbackSamples,
                _gpuCallbackTotalMs,
                _gpuCallbackMaxMs,
                _renderAndSnapshotTotalMs,
                _blitTotalMs,
                _flushTotalMs,
                _gpuSynchronizationSamples,
                _gpuLockWaitTotalMs,
                _gpuLockWaitMaxMs,
                _gpuLockHeldTotalMs,
                _gpuLockHeldMaxMs,
                layers, _replayTotalMs, _replayMaxMs, _ageTotalMs,
                _publishedSnapshots, _droppedSnapshots, _snapshotSlotsInUse);

            _sceneRenderSamples = 0;
            _sceneRenderTotalMs = 0;
            _sceneRenderMaxMs = 0;
            _queryAndSortTotalMs = 0;
            _drawTotalMs = 0;
            _overlayTotalMs = 0;
            _visibleDrawableTotal = 0;
            _visibleTileTotal = 0;
            _gpuCallbackSamples = 0;
            _gpuCallbackTotalMs = 0;
            _gpuCallbackMaxMs = 0;
            _renderAndSnapshotTotalMs = 0;
            _blitTotalMs = 0;
            _flushTotalMs = 0;
            _gpuSynchronizationSamples = 0;
            _replayTotalMs = _replayMaxMs = _ageTotalMs = 0;
            _gpuLockWaitTotalMs = 0;
            _gpuLockWaitMaxMs = 0;
            _gpuLockHeldTotalMs = 0;
            _gpuLockHeldMaxMs = 0;
            _layerDiagnostics.Clear();

            return snapshot;
        }
    }

    private void OnViewportTargetRectChanged(ViewportResizedEventArgs args)
    {
        if (Engine.IsDisposed)
            return;

        Engine.EngineDispatcher.Post(() =>
        {
            if (_diagnosticsText is not null)
                _diagnosticsText.ScreenBounds = GetDiagnosticsBounds(args.NewRect);
        });
    }

    private static Rectangle GetDiagnosticsBounds(Rectangle viewport)
    {
        int width = Math.Min(
            DiagnosticsWidth,
            Math.Max(1, viewport.Width - DiagnosticsMargin * 2));
        int height = Math.Min(
            DiagnosticsHeight,
            Math.Max(1, viewport.Height - DiagnosticsMargin * 2));

        return new Rectangle(
            viewport.Left + DiagnosticsMargin,
            viewport.Top + DiagnosticsMargin,
            width,
            height);
    }

    private static double Average(double total, long count) =>
        count > 0 ? total / count : 0d;

    private static double Average(long total, long count) =>
        count > 0 ? (double)total / count : 0d;

    private static void RecordMaximum(ref long target, long value)
    {
        long current = Volatile.Read(ref target);
        while (value > current)
        {
            long observed = Interlocked.CompareExchange(ref target, value, current);
            if (observed == current)
                return;

            current = observed;
        }
    }

    protected override void OnDisposed()
    {
        // GameHostBase disposes only an initialized Engine. Also cover failure
        // during content loading, before its engine-initialized flag was set.
        if (!Engine.IsDisposed)
            Engine.Dispose();
        (Engine.Input.KeyboardEventPoller?.Adapter as IDisposable)?.Dispose();
    }

    private sealed class LayerDiagnosticsWindow(int layerIndex, string layerId, int zOrder)
    {
        internal int LayerIndex { get; } = layerIndex;
        internal string LayerId { get; } = layerId;
        internal int ZOrder { get; } = zOrder;
        internal int TransformedTileCount { get; private set; }
        internal int TileWidth { get; private set; }
        internal int TileHeight { get; private set; }
        internal long Samples { get; private set; }
        internal double QueryTotalMs { get; private set; }
        internal double DrawTotalMs { get; private set; }
        internal long DrawableTotal { get; private set; }
        internal long TileTotal { get; private set; }

        internal void Add(GpuLayerRenderDiagnostics diagnostics)
        {
            Samples++;
            QueryTotalMs += diagnostics.QueryAndSortMilliseconds;
            DrawTotalMs += diagnostics.DrawMilliseconds;
            DrawableTotal += diagnostics.DrawableCount;
            TileTotal += diagnostics.TileCount;
            TransformedTileCount = diagnostics.TransformedTileCount;
            TileWidth = diagnostics.TileWidth;
            TileHeight = diagnostics.TileHeight;
        }

        internal LayerDiagnosticsSnapshot ToSnapshot() =>
            new(
                LayerIndex,
                LayerId,
                ZOrder,
                Samples,
                QueryTotalMs,
                DrawTotalMs,
                DrawableTotal,
                TileTotal,
                TransformedTileCount,
                TileWidth,
                TileHeight);
    }

    private sealed record LayerDiagnosticsSnapshot(
        int LayerIndex,
        string LayerId,
        int ZOrder,
        long Samples,
        double QueryTotalMs,
        double DrawTotalMs,
        long DrawableTotal,
        long TileTotal,
        int TransformedTileCount,
        int TileWidth,
        int TileHeight);

    private sealed record RenderDiagnosticsSnapshot(
        long SceneRenderSamples,
        double SceneRenderTotalMs,
        double SceneRenderMaxMs,
        double QueryAndSortTotalMs,
        double DrawTotalMs,
        double OverlayTotalMs,
        long VisibleDrawableTotal,
        long VisibleTileTotal,
        long GpuCallbackSamples,
        double GpuCallbackTotalMs,
        double GpuCallbackMaxMs,
        double RenderAndSnapshotTotalMs,
        double BlitTotalMs,
        double FlushTotalMs,
        long GpuSynchronizationSamples,
        double GpuLockWaitTotalMs,
        double GpuLockWaitMaxMs,
        double GpuLockHeldTotalMs,
        double GpuLockHeldMaxMs,
        IReadOnlyList<LayerDiagnosticsSnapshot> Layers,
        double ReplayTotalMs, double ReplayMaxMs, double AgeTotalMs,
        long PublishedSnapshots, long DroppedSnapshots, int SnapshotSlotsInUse);

    // Viewer startup must not load an unrelated game config/state from the cwd.
    internal sealed class ViewerConfiguration : IEngineConfigurationStore
    {
        public EngineConfiguration Configuration { get; } = new();
        public bool AutoSave { get; set; }
        public void Save() { }
        public void Dispose() { }
    }
}
