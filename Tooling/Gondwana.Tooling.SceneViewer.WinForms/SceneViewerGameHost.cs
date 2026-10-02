using System.Text;
using Gondwana.Configuration;
using Gondwana.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Timers;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using SkiaSharp;
using GondwanaMouseEventArgs = Gondwana.Input.Mouse.MouseEventArgs;

namespace Gondwana.Tooling.SceneViewer.WinForms;

internal sealed class SceneViewerGameHost(WinFormGpuRenderSurfaceControl surface, string scenePath)
    : WinFormsGpuGameHost(surface)
{
    private const int DiagnosticsMargin = 12;
    private const int DiagnosticsWidth = 520;
    private const int DiagnosticsHeight = 320;

    private readonly HashSet<Keys> _keysDown = [];
    private long _lastTick;
    private long _backgroundStartTick;
    private long _backgroundTotalTicks;
    private long _backgroundMaxTicks;
    private long _backgroundSampleCount;
    private int _animatingTileCount;
    private View? _view;
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
        Keys.F3
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
                ResetBackgroundDiagnosticsWindow();
        });
    }

    private void ToggleDiagnostics()
    {
        if (_diagnosticsText is null)
            return;

        _diagnosticsText.Visible = !_diagnosticsText.Visible;

        if (_diagnosticsText.Visible)
            UpdateDiagnosticsText(_lastCpsSample);
    }

    private void UpdateDiagnosticsText(Gondwana.CyclesPerSecondCalculatedEventArgs? sample)
    {
        if (_diagnosticsText is null || _view is null)
            return;

        (double averageBackgroundMs, double maxBackgroundMs, long backgroundSamples) =
            ResetBackgroundDiagnosticsWindow();

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

        var text = new StringBuilder()
            .AppendLine("Gondwana Scene Viewer Diagnostics  [F3]")
            .AppendLine($"Scene: {Path.GetFileName(scenePath)}")
            .AppendLine($"CPS: {(sample?.GrossCPS ?? 0):0.0}")
            .AppendLine($"Engine FPS: {(sample?.NetCPS ?? 0):0.0}")
            .AppendLine($"GPU FPS: {gpuFps}")
            .AppendLine($"Background work avg/max: {averageBackgroundMs:0.000} / {maxBackgroundMs:0.000} ms")
            .AppendLine($"Background samples: {backgroundSamples:N0}")
            .AppendLine($"Animating tiles: {Volatile.Read(ref _animatingTileCount):N0}")
            .AppendLine($"Layers / grid cells: {layerCount:N0} / {gridTileCount:N0}")
            .AppendLine($"Camera: {cameraPosition.X:0.0}, {cameraPosition.Y:0.0} px")
            .AppendLine($"Zoom: {_view.Viewport.Zoom:0.000}x")
            .AppendLine($"Viewport: {viewport.Width} x {viewport.Height}")
            .AppendLine($"Backbuffer: {backbuffer.Width} x {backbuffer.Height}")
            .AppendLine($"Target FPS / VSync: {Engine.Configuration.TargetFPS} / {(Engine.Configuration.VSync ? "on" : "off")}")
            .Append($"MSAA requested / actual / max: {msaa}")
            .ToString();

        _diagnosticsText.SetText(text);
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
            viewport.Right - width - DiagnosticsMargin,
            viewport.Top + DiagnosticsMargin,
            width,
            height);
    }

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

    // Viewer startup must not load an unrelated game config/state from the cwd.
    internal sealed class ViewerConfiguration : IEngineConfigurationStore
    {
        public EngineConfiguration Configuration { get; } = new();
        public bool AutoSave { get; set; }
        public void Save() { }
        public void Dispose() { }
    }
}
