using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Gamepad;
using Gondwana.Logging;
using Gondwana.Scenes;
using Gondwana.WinForms;
using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.CoordinateTest;

public class Game : IDisposable
{
    public WinFormBitmapRenderSurfaceControl RenderSurface { get; private set; }

    public Scene? Scene { get; private set; }

    public Game(WinFormBitmapRenderSurfaceControl renderSurface)
    {
        RenderSurface = renderSurface;
    }

    public void InitializeGame(string? configPath = null, bool? autoSaveConfig = null)
    {
        EngineLogger.SetLogLevel(LogLevel.Trace);

        // initialize engine, platform-specific adapters, etc.
        Engine.Instance.Initialize(configPath, autoSaveConfig);
        Engine.Instance.InitializeWinFormsAudioFormats();

        // load game content here
        LoadAssets();
        LoadTilesheets();
        LoadAnimationCycles();
        InitSprites();
        InitDirectDrawings();

        // create initial scene here and bind to render surface
        Scene = CreateInitialScene();
        RenderSurface.Host.Bind(Scene);

        // configure input handling here
        ConfigureKeyboardInput();
        ConfigureMouseInput();
        ConfigureGamepadInput();

        // start the engine main loop
        Engine.Instance.Start(SynchronizationContext.Current!);

        RenderSurface.Host.ViewRenderer.AddView(new Rectangle(800, 0, 800, 900), 1f);
        RenderSurface.Host.ViewRenderer.Views[0].Camera.SnapTo(new PointF(-100, -100));
        RenderSurface.Host.ViewRenderer.Views[1].Camera.SnapTo(new PointF(-100, -100));
        RenderSurface.Host.RedrawDirtyRectangleOnly = false;

        DumpCoordinateDebug();
    }

    #region load and init game content

    private void LoadAssets()
    {
        // load asset files

        // load standalone audio files

        // load standalone image files

        // load standalone video viles

        // load standalone cursor files
    }

    private void LoadTilesheets()
    {
        // Implementation for loading tilesheets goes here
    }

    private void LoadAnimationCycles()
    {
        // Implementation for loading animation cycles goes here
    }

    private void InitSprites()
    {
        // Implementation for creating sprites goes here
    }

    private DirectRectangle? _directRectangle;
    private TextBlock? _textBlockCPS;
    private TextBlock? _textBlockMouse;

    private void InitDirectDrawings()
    {
        // Implementation for creating direct drawings goes here
        _directRectangle = new DirectRectangle(RenderSurface.Host,
                                               new Rectangle(RenderSurface.Size.Width - 250, 0, 250, 150),
                                               Color.Wheat);
        _directRectangle.SetFilled(true);

        _textBlockCPS = new TextBlock(RenderSurface.Host,_directRectangle.Bounds);
        _textBlockCPS.SetColors(Color.Black, Color.Transparent).ZOrder = 10;

        Engine.Instance.CPSCalculated += (e) =>
        {
            _textBlockCPS.SetText(e.ToString());
        };

        _textBlockMouse = new TextBlock(RenderSurface.Host, new Rectangle(RenderSurface.Size.Width - 250, 200, 250, 150));
        _textBlockMouse.SetColors(Color.Black, Color.Wheat).ZOrder = 10;
    }

    #endregion load and init game content

    private Scene? CreateInitialScene()
    {
        var scene = new Scene();
        scene.AddLayer(60, 5, 64, 64, 1, CoordinateSystemTypes.HexFlatTop);

        scene!.SceneLayers[0].ShowGridLines = true;

        return scene;
    }

    #region input configuration

    private void ConfigureKeyboardInput()
    {
        Engine.Instance.InitializeWinFormsKeyboardAdapter(RenderSurface);
        Engine.KeyboardEventPoller!.KeyDown += KeyboardEventPoller_KeyDown;
        Engine.KeyboardEventPoller.StartMonitoringKey("w");
        Engine.KeyboardEventPoller.StartMonitoringKey("a");
        Engine.KeyboardEventPoller.StartMonitoringKey("s");
        Engine.KeyboardEventPoller.StartMonitoringKey("d");
    }

    private void KeyboardEventPoller_KeyDown(Input.Keyboard.KeyDownEventArgs args)
    {
        // Handle key down events here
    }

    private void ConfigureMouseInput()
    {
        Engine.Instance.InitializeWinFormsMouseAdapter(RenderSurface);
        Engine.MouseEventPoller!.MouseEvent += MouseEventPoller_MouseEvent;
        Engine.MouseEventPoller.StartMonitoringMouse();
    }

    private void MouseEventPoller_MouseEvent(Input.Mouse.MouseEventArgs args)
    {
        var view = RenderSurface.Host.ViewRenderer.Views[0];
        var layer = Scene!.SceneLayers[0];
        var vp = view.Viewport;

        // Undo the viewport's translate + scale to get world-pixel coords
        float zoom = (vp.Zoom <= 0f ? 1f : vp.Zoom);

        var worldPos = new PointF(
            (args.CurrentPosition.X - vp.TargetRectPx.Left - vp.ScreenOffsetPx.X) * zoom,
            (args.CurrentPosition.Y - vp.TargetRectPx.Top - vp.ScreenOffsetPx.Y) * zoom
        );

        // Now use the layer's coordinate system to get the tile
        var gridPos = layer.CoordinateSystem.GetSceneLayerCoordinatesAtPixel(layer, worldPos);

        var message =
            $"Anchor Col/Row: {layer.RenderSurfaceOriginCoordinates}\n" +
            $"Mouse Pos (screen): {args.CurrentPosition.X}, {args.CurrentPosition.Y}\n" +
            $"World Pos (px): {worldPos.X:F1}, {worldPos.Y:F1}\n" +
            $"Grid coordinates: {gridPos.X}, {gridPos.Y}";

        _textBlockMouse?.SetText(message);
    }

    private void ConfigureGamepadInput()
    {
        //Engine.Instance.InitializeSdlGamepadManager();

        Engine.Instance.InitializeXInputGamepadManager();
        Engine.GamepadEventPoller!.ButtonDown += GamepadEventPoller_ButtonDown;

        foreach (var gamepadAdapter in Engine.GamepadManager!.ConnectedAdapters)
        {
            Engine.GamepadEventPoller.StartMonitoringButton(gamepadAdapter.GamepadId, "");
        }
    }

    private void GamepadEventPoller_ButtonDown(GamepadButtonDownEventArgs args)
    {
        // Handle gamepad button down events here
    }

    #endregion input configuration

    #region IDisposable support

    private bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Engine.KeyboardEventPoller!.KeyDown -= KeyboardEventPoller_KeyDown;
                Engine.MouseEventPoller!.MouseEvent -= MouseEventPoller_MouseEvent;
                Engine.GamepadEventPoller!.ButtonDown -= GamepadEventPoller_ButtonDown;

                // Dispose managed resources
                Engine.Instance.Stop();
                Engine.Instance.Dispose();
            }

            // Free unmanaged resources (if any) here

            disposedValue = true;
        }
    }

    ~Game()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion IDisposable support

    private void DumpCoordinateDebug()
    {
        var layer = Scene!.SceneLayers[0];
        var view = RenderSurface.Host.ViewRenderer.Views[0];

        // 1) Raw screen center
        var screen = new PointF(
            RenderSurface.Width / 2,
            RenderSurface.Height / 2);

        // 2) Convert screen to world
        var worldFromScreen = view.ScreenToWorldPx(screen);

        // 3) Convert screen directly (wrong path)
        var grid_viaScreen = layer.CoordinateSystem
            .GetSceneLayerCoordinatesAtPixel(layer, screen);

        // 4) Convert world (intended path)
        var grid_viaWorld = layer.CoordinateSystem
            .GetSceneLayerCoordinatesAtPixel(layer, worldFromScreen);

        // 5) Log everything
        Engine.Logger.LogDebug("---- COORD DEBUG ----");
        Engine.Logger.LogDebug($"Camera.PosPx: {view.Camera.PositionPx}");
        Engine.Logger.LogDebug($"Layer.OriginPx: {layer.RenderSurfaceOriginPx}");
        Engine.Logger.LogDebug($"Screen: {screen}");
        Engine.Logger.LogDebug($"WorldFromScreen: {worldFromScreen}");
        Engine.Logger.LogDebug($"Grid via SCREEN: {grid_viaScreen}");
        Engine.Logger.LogDebug($"Grid via WORLD: {grid_viaWorld}");
        Engine.Logger.LogDebug("----------------------");
    }
}
