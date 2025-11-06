using Gondwana.Drawing.Coordinates;
using Gondwana.Input.Gamepad;
using Gondwana.Logging;
using Gondwana.Scenes;
using Gondwana.WinForms;
using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.CoordinateTest;

public class Game : IDisposable
{
    private bool disposedValue;

    public WinFormBitmapRenderSurfaceControl RenderSurface { get; private set; }

    public Scene? Scene { get; private set; }

    public Game(WinFormBitmapRenderSurfaceControl renderSurface)
    {
        RenderSurface = renderSurface;
        InitializeEngine();
    }

    private void InitializeEngine(string? configPath = null, bool? autoSaveConfig = null)
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

        Scene!.SceneLayers[0].ShowGridLines = true;

        // configure input handling here
        ConfigureKeyboardInput();
        ConfigureMouseInput();
        ConfigureGamepadInput();

        // start the engine main loop
        Engine.Instance.Start(SynchronizationContext.Current!);
    }

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
        // Handle mouse events here
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

    private void InitDirectDrawings()
    {
        // Implementation for creating direct drawings goes here
    }

    private Scene? CreateInitialScene()
    {
        var scene = new Scene();
        SceneLayer sceneLayer = new SceneLayer(100, 90, 64, 64);
        sceneLayer.CoordinateSystem = new HexagonalPointedTopCoordinates();
        scene.AddLayer(sceneLayer);
        return scene;
    }

    #region IDisposable support
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
}
