using Gondwana.Configuration;
using Gondwana.Input.Keyboard;
using Gondwana.Scenes;
using Gondwana.Timers;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using GondwanaMouseEventArgs = Gondwana.Input.Mouse.MouseEventArgs;

namespace Gondwana.Tooling.SceneViewer.WinForms;

internal sealed class SceneViewerGameHost(WinFormGpuRenderSurfaceControl surface, string scenePath)
    : WinFormsGpuGameHost(surface)
{
    private readonly HashSet<Keys> _keysDown = [];
    private long _lastTick;

    internal ViewerCameraController? Camera { get; private set; }

    internal event Action? CloseRequested;

    protected override Scene CreateInitialScene() => new ViewerSceneLoader().Load(scenePath);

    protected override void OnSceneBound()
    {
        RenderSurface.Host.ViewManager.ConfigureSingleFullView();
        var view = RenderSurface.Host.ViewManager.Views[0];
        view.Camera.WorldBoundsPx = RectangleF.Empty;
        view.Camera.SnapTo(PointF.Empty);

        var zoomLayer = Scene!.SceneLayers
            .Where(layer => layer.Visible && Math.Abs(layer.Parallax) > 1e-6f)
            .OrderBy(layer => Math.Abs(layer.Parallax - 1f))
            .ThenByDescending(layer => layer.ZOrder)
            .FirstOrDefault()
            ?? Scene.SceneLayers.FirstOrDefault();

        Camera = new ViewerCameraController(view, zoomLayer);
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
        Engine.BeforeBackgroundTasksExecute += UpdateCamera;
    }

    protected override void ConfigureGamepads() { }

    protected override void UnhookEvents()
    {
        if (Engine.Input.KeyboardEventPoller is not null)
            Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;

        if (Engine.Input.MouseEventPoller is not null)
            Engine.Input.MouseEventPoller.MouseEvent -= OnMouse;

        Engine.BeforeBackgroundTasksExecute -= UpdateCamera;
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
        Keys.Escape
    ];

    private void OnKeyDown(KeyDownEventArgs args)
    {
        if (!Enum.TryParse(args.KeyConfig.Key, true, out Keys key))
            return;

        if (args.KeyAction == KeyAction.Pressed)
        {
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
