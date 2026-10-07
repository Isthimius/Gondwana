using Gondwana.Configuration;
using Gondwana.Drawing;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Timers;
using Gondwana.Widgets.Hud;
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

    private readonly HashSet<Keys> _keysDown = [];
    private long _lastTick;
    private bool _animationsPaused;
    private Tilesheet? _stressTilesheet;
    private SceneLayer? _stressLayer;
    private GondwanaView? _view;
    private ProfilerWidget? _diagnosticsWidget;


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

        _diagnosticsWidget = new ProfilerWidget(
            RenderSurface.Host,
            _view,
            GetDiagnosticsBounds(_view.Viewport.TargetRectPx),
            "scene-viewer-diagnostics")
        {
            HeaderText = "Gondwana Scene Viewer Diagnostics  [F3]",
            ContextInfo = ProfilerContextInfo.All,
            ShowSnapshotMetadata = false,
            AdditionalLinesProvider = GetSceneViewerDiagnosticLines
        };

        _diagnosticsWidget.Display.TextBlock.LineSpacingMultiplier = 1.05f;
        _diagnosticsWidget.SetProfilerZOrder(20_000);

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

        _diagnosticsWidget?.Dispose();
        _diagnosticsWidget = null;

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

        if (_diagnosticsWidget?.Visible == true)
            _diagnosticsWidget.Refresh();
    }

    private void ToggleDiagnostics()
    {
        if (_diagnosticsWidget is null)
            return;

        if (_diagnosticsWidget.Visible)
            _diagnosticsWidget.Hide();
        else
            _diagnosticsWidget.Show();
    }

    private IEnumerable<string> GetSceneViewerDiagnosticLines(
        ProfilerWidgetExtensionContext context)
    {
        _ = context;

        yield return stress is null
            ? $"Scene: {Path.GetFileName(scenePath)}"
            : $"Stress: {stress.TileCount:N0} tiles / {stress.Projection}";
        yield return $"Animations: {(_animationsPaused ? "PAUSED" : "running")}  [F4]";
    }

    private void OnViewportTargetRectChanged(ViewportResizedEventArgs args)
    {
        if (Engine.IsDisposed)
            return;

        Engine.EngineDispatcher.Post(() =>
        {
            if (_diagnosticsWidget is not null)
            {
                Rectangle bounds = GetDiagnosticsBounds(args.NewRect);
                _diagnosticsWidget.SetPosition(bounds.X, bounds.Y);
                _diagnosticsWidget.Size = bounds.Size;
            }
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
