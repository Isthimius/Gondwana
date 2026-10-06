using System.Text;
using Gondwana.Configuration;
using Gondwana.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
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

internal sealed class SceneViewerGameHost(
    WinFormGpuRenderSurfaceControl surface,
    string? scenePath,
    SceneViewerStressOptions? stress = null)
    : WinFormsGpuGameHost(surface)
{
    private const int StressTileSize = 16;
    private const int DiagnosticsMargin = 12;
    private const int DiagnosticsWidth = 700;
    private const int DiagnosticsHeight = 760;
    private const int MaxDiagnosticLayers = 8;

    private readonly HashSet<Keys> _keysDown = [];
    private long _lastTick;
    private long _lastDiagnosticsTick;
    private IDisposable? _telemetryRequest;
    private bool _animationsPaused;
    private Tilesheet? _stressTilesheet;
    private SceneLayer? _stressLayer;
    private GondwanaView? _view;
    private TextBlock? _diagnosticsText;


    internal ViewerCameraController? Camera { get; private set; }

    internal event Action? CloseRequested;

    protected override void LoadTilesheets()
    {
        if (stress is null)
            return;

        var bitmap = new SKBitmap(StressTileSize * 2, StressTileSize * 2);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint())
        {
            SKColor[] colors =
            [
                new(54, 123, 245),
                new(245, 183, 54),
                new(66, 176, 96),
                new(198, 76, 130)
            ];

            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    paint.Color = colors[y * 2 + x];
                    canvas.DrawRect(
                        x * StressTileSize,
                        y * StressTileSize,
                        StressTileSize,
                        StressTileSize,
                        paint);
                }
            }
        }

        _stressTilesheet = Engine.Managers.Tilesheets.LoadFromBitmap(
            $"scene-viewer-stress-{Guid.NewGuid():N}",
            bitmap);
        _stressTilesheet.DefaultRegion.TileSize = new Size(StressTileSize, StressTileSize);
    }

    protected override Scene CreateInitialScene()
    {
        if (stress is null)
            return new ViewerSceneLoader().Load(scenePath!);

        if (_stressTilesheet is null)
            throw new InvalidOperationException("Stress tilesheet was not initialized.");

        // Shape the grid near the viewport's 4:3 aspect ratio so fitting it does
        // not waste large areas of the screen. The final row may contain a few blanks.
        int columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(stress.TileCount * 4d / 3d)));
        int rows = (int)Math.Ceiling(stress.TileCount / (double)columns);

        var scene = new Scene();
        _stressLayer = scene.AddLayer(
            columns,
            rows,
            StressTileSize,
            StressTileSize,
            coordinateSystem: stress.Projection);
        Frame[] frames =
        [
            _stressTilesheet.GetFrame(0, 0),
            _stressTilesheet.GetFrame(1, 0),
            _stressTilesheet.GetFrame(0, 1),
            _stressTilesheet.GetFrame(1, 1)
        ];

        for (int i = 0; i < stress.TileCount; i++)
        {
            int x = i % columns;
            int y = i / columns;
            _stressLayer[x, y]!.CurrentFrame = frames[(x + y) & 3];
        }

        return scene;
    }

    protected override void OnSceneBound()
    {
        RenderSurface.Host.ViewManager.ConfigureSingleFullView();
        _view = RenderSurface.Host.ViewManager.Views[0];
        _view.Camera.WorldBoundsPx = RectangleF.Empty;

        if (stress is not null && _stressLayer is not null)
            FitStressLayer(_view, _stressLayer);
        else
            _view.Camera.SnapTo(PointF.Empty);

        var zoomLayer = Scene!.SceneLayers
            .Where(layer => layer.Visible && Math.Abs(layer.Parallax) > 1e-6f)
            .OrderBy(layer => Math.Abs(layer.Parallax - 1f))
            .ThenByDescending(layer => layer.ZOrder)
            .FirstOrDefault()
            ?? Scene.SceneLayers.FirstOrDefault();

        Camera = new ViewerCameraController(_view, zoomLayer);
    }

    private static void FitStressLayer(GondwanaView view, SceneLayer layer)
    {
        var bounds = layer.GetLayerBoundsPx();
        var viewport = view.Viewport.TargetRectPx;
        if (bounds.IsEmpty || viewport.Width <= 0 || viewport.Height <= 0)
            return;

        float zoom = Math.Min(
            viewport.Width / bounds.Width,
            viewport.Height / bounds.Height) * .95f;
        zoom = Math.Clamp(
            zoom,
            ViewerCameraController.MinimumZoom,
            ViewerCameraController.MaximumZoom);

        view.Viewport.SnapZoom(zoom);
        var visible = view.Viewport.VisibleWorldSizePx;
        view.Camera.SnapTo(new PointF(
            bounds.Left + (bounds.Width - visible.Width) / 2f,
            bounds.Top + (bounds.Height - visible.Height) / 2f));
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
            .SetColors(SKColors.White, new SKColor(0, 0, 0, 102))
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

    /// <inheritdoc/>
    protected override void OnEngineInitialized()
    {
        _lastTick = HighResTimer.GetCurrentTick();
        Engine.BeforeBackgroundTasksExecute += BeforeBackgroundTasksExecute;
        Engine.AfterBackgroundTasksExecute += AfterBackgroundTasksExecute;

    }

    protected override void ConfigureGamepads() { }

    /// <inheritdoc/>
    protected override void UnhookEvents()
    {
        if (Engine.Input.KeyboardEventPoller is not null)
            Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;

        if (Engine.Input.MouseEventPoller is not null)
            Engine.Input.MouseEventPoller.MouseEvent -= OnMouse;

        Engine.BeforeBackgroundTasksExecute -= BeforeBackgroundTasksExecute;
        Engine.AfterBackgroundTasksExecute -= AfterBackgroundTasksExecute;
        _telemetryRequest?.Dispose();
        _telemetryRequest = null;


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

    private void BeforeBackgroundTasksExecute() => UpdateCamera();

    private void AfterBackgroundTasksExecute()
    {
        if (_telemetryRequest is null) return;
        long now = HighResTimer.GetCurrentTick();
        if (HighResTimer.GetDuration(_lastDiagnosticsTick, now) < .25) return;
        _lastDiagnosticsTick = now;
        UpdateDiagnosticsText();
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
            UpdateDiagnosticsText();
    }

    private void ToggleDiagnostics()
    {
        if (_diagnosticsText is null) return;
        _diagnosticsText.Visible = !_diagnosticsText.Visible;
        if (_diagnosticsText.Visible)
        {
            _telemetryRequest = Engine.Profiler.Start();
            UpdateDiagnosticsText();
        }
        else
        {
            _telemetryRequest?.Dispose();
            _telemetryRequest = null;
        }
    }

    private void UpdateDiagnosticsText()
    {
        if (_diagnosticsText is null || _view is null) return;
        var snapshot = Engine.Profiler.GetLatestSnapshot();
        var core = snapshot?.Sources.FirstOrDefault(s => s.Backend == "Core");
        var render = snapshot?.Sources.FirstOrDefault(s => s.Id == RenderSurface.Host.Telemetry?.Id);
        string Value(Gondwana.Diagnostics.TelemetrySourceSnapshot? source, string key, bool last = false)
        {
            if (source is null || !source.Metrics.TryGetValue(key, out var metric)) return "NotYetSampled";
            return metric.Availability == Gondwana.Diagnostics.TelemetryAvailability.Available
                ? (last ? metric.Last : metric.Mean)?.ToString(last ? "0" : key.EndsWith(".ms", StringComparison.Ordinal) ? "0.000" : "0.0") ?? "NotYetSampled"
                : metric.Availability.ToString();
        }
        string Rate(Gondwana.Diagnostics.TelemetrySourceSnapshot? source, string key)
            => snapshot is { ElapsedSeconds: > 0 } && source?.Metrics.TryGetValue(key, out var metric) == true
                && metric.Count > 0 ? (metric.Count / snapshot.ElapsedSeconds).ToString("0.0") : "NotYetSampled";
        string Timing(Gondwana.Diagnostics.TelemetrySourceSnapshot? source, string key)
            => source?.Metrics.TryGetValue(key, out var metric) == true && metric.Count > 0
                ? $"{metric.Mean:0.000} / {metric.Maximum:0.000} ms ({metric.Count:N0})" : Value(source, key);

        var viewport = _view.Viewport.TargetRectPx;
        var position = _view.Camera.PositionPx;
        var backbuffer = RenderSurface.Host.Backbuffer;
        var text = new StringBuilder()
            .AppendLine("Gondwana Scene Viewer Diagnostics  [F3]")
            .AppendLine(stress is null ? $"Scene: {Path.GetFileName(scenePath)}" : $"Stress: {stress.TileCount:N0} tiles / {stress.Projection}")
            .AppendLine($"Animations: {(_animationsPaused ? "PAUSED" : "running")}  [F4]")
            .AppendLine($"CPS / foreground / presentation: {Rate(core, "cycle.cpu.ms")} / {Rate(core, "foreground.cpu.ms")} / {Rate(render, "presentation.count")}")
            .AppendLine($"Background avg/max: {Timing(core, "background.cpu.ms")}")
            .AppendLine($"GL callback avg/max: {Timing(render, "presentation.cpu.ms")}")
            .AppendLine($"Render+snapshot avg: {Value(render, "render.snapshot.cpu.ms")} ms")
            .AppendLine($"GL gate wait / held: {Value(render, "gate.wait.cpu.ms")} / {Value(render, "gate.held.cpu.ms")}")
            .AppendLine($"Snapshot build avg/max: {Timing(render, "build.cpu.ms")}")
            .AppendLine($"Build query / sort: {Value(render, "query.cpu.ms")} / {Value(render, "sort.cpu.ms")} ms")
            .AppendLine($"Command / overlay record: {Value(render, "record.cpu.ms")} / {Value(render, "overlay.cpu.ms")} ms")
            .AppendLine($"GL replay avg/max: {Timing(render, "replay.cpu.ms")}")
            .AppendLine($"Picture / backbuffer flush / snapshot: {Value(render, "picture.cpu.ms")} / {Value(render, "backbuffer.flush.cpu.ms")} / {Value(render, "snapshot.cpu.ms")} ms")
            .AppendLine($"Snapshot age: {Value(render, "snapshot.age.ms")} ms")
            .AppendLine($"Published / dropped / slots / commands: {Value(render, "mailbox.published.lifetime", true)} / {Value(render, "mailbox.dropped.lifetime", true)} / {Value(render, "mailbox.slots", true)} / {Value(render, "snapshot.commands.approximate", true)}")
            .AppendLine($"Blit / final flush: {Value(render, "blit.cpu.ms")} / {Value(render, "flush.cpu.ms")} ms")
            .AppendLine($"Visible draw instances / tiles: {Value(render, "visible.drawables")} / {Value(render, "visible.tiles")}")
            .AppendLine($"Atlas batches / tiles: {Value(render, "atlas.batches")} / {Value(render, "atlas.tiles")}")
            .AppendLine($"Animating tiles: {Tile.TilesAnimating.Count:N0}")
            .AppendLine($"Layers / grid cells: {Scene?.SceneLayers.Count ?? 0} / {Scene?.SceneLayers.Sum(l => (long)l.GridColumnCount * l.GridRowCount) ?? 0}")
            .AppendLine($"Camera: {position.X:0.0}, {position.Y:0.0} px; zoom: {_view.Viewport.Zoom:0.000}x")
            .AppendLine($"Viewport / backbuffer: {viewport.Width}x{viewport.Height} / {backbuffer.Width}x{backbuffer.Height}")
            .AppendLine($"Target FPS / VSync: {Engine.Configuration.TargetFPS} / {Engine.Configuration.VSync}");
        if (backbuffer is GpuBackbuffer gpu)
            text.AppendLine($"MSAA requested / actual / max: {gpu.MsaaSampleCount} / {gpu.ActualMsaaSampleCount} / {gpu.MaxSupportedMsaaSampleCount}");
        for (int i = 0; i < MaxDiagnosticLayers; i++)
        {
            if (render?.Metrics.TryGetValue($"layer.{i}.query.cpu.ms", out var layer) != true || layer.Count == 0) continue;
            text.AppendLine($"L{i} z{Value(render, $"layer.{i}.z", true)} {Value(render, $"layer.{i}.tile.width.px", true)}x{Value(render, $"layer.{i}.tile.height.px", true)} xform={Value(render, $"layer.{i}.transformed.tiles", true)}: q/rec {Value(render, $"layer.{i}.query.cpu.ms")} / {Value(render, $"layer.{i}.record.cpu.ms")} ms; draw/tiles {Value(render, $"layer.{i}.drawables")} / {Value(render, $"layer.{i}.tiles")}");
        }
        if (snapshot?.Truncated == true) text.AppendLine("Telemetry detail truncated.");
        _diagnosticsText.SetText(text.ToString().TrimEnd());
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

    protected override void OnDisposed()
    {
        _stressTilesheet?.Dispose();
        _stressTilesheet = null;

        // GameHostBase disposes only an initialized Engine. Also cover failure
        // during content loading, before its engine-initialized flag was set.
        if (!Engine.IsDisposed)
            Engine.Dispose();
        (Engine.Input.KeyboardEventPoller?.Adapter as IDisposable)?.Dispose();
    }

    // Viewer startup must not load an unrelated game config/state from the cwd.
    // Keep Engine foreground work uncapped and preserve the viewer's GPU VSync default.
    /// <summary>Provides standalone viewer defaults without persisting unrelated game configuration.</summary>
    internal sealed class ViewerConfiguration : IEngineConfigurationStore
    {
        /// <inheritdoc/>
        public EngineConfiguration Configuration { get; } = new()
        {
            TargetFPS = 0,
            VSync = true
        };

        /// <inheritdoc/>
        public bool AutoSave { get; set; }
        /// <inheritdoc/>
        public void Save() { }
        /// <inheritdoc/>
        public void Dispose() { }
    }
}
