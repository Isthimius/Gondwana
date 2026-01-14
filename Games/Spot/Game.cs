using System;
using System.Threading;
using System.Windows.Forms;
using Gondwana;
using Gondwana.Drawing.Coordinates;
using Gondwana.Input.Gamepad;
using Gondwana.Input.Keyboard;
using Gondwana.Logging;
using Gondwana.Scenes;
using Gondwana.WinForms;
using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace HWG.Spot;

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

        // create initial scene here and bind to render surface
        Scene = CreateInitialScene();
        CreateInitialViews();
        RenderSurface.Host.Bind(Scene, false);

        // initialize sprites and direct drawings
        InitSprites();
        InitDirectDrawings();

        // configure input handling here
        ConfigureKeyboardInput();
        ConfigureMouseInput();
        ConfigureGamepadInput();

        // start the engine main loop
        Engine.Instance.Start(SynchronizationContext.Current!);
    }

    #region load and init game content

    private void LoadAssets()
    {
        // load asset files

        // load standalone audio files

        // load standalone image files

        // load standalone video files

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

    private Scene CreateInitialScene()
    {
        // Implementation for loading scenes goes here
        var scene = new Scene();
        var sceneLayer1 = scene.AddLayer(60, 5, 64, 64, 10, 1f, CoordinateSystemTypes.Orthographic);

        sceneLayer1.ShowGridLines = true;

        //var sourceTilesheet = TilesheetRegistry.Instance.GetAll()["tiles"];

        return scene;
    }

    private void CreateInitialViews()
    {
        // Implementation for creating views (other than the default full-GameWindow) goes here
    }

    private void InitSprites()
    {
        // Implementation for creating sprites goes here
    }

    private void InitDirectDrawings()
    {
        //Implementation for creating direct drawings goes here
    }

    #endregion load and init game content

    #region input configuration

    private void ConfigureKeyboardInput()
    {
        Engine.Instance.InitializeWinFormsKeyboardAdapter(RenderSurface);
        Engine.KeyboardEventPoller!.KeyDown += KeyboardEventPoller_KeyDown;
        Engine.KeyboardEventPoller.StartMonitoringKey((int)Keys.W);
        Engine.KeyboardEventPoller.StartMonitoringKey((int)Keys.A);
        Engine.KeyboardEventPoller.StartMonitoringKey((int)Keys.S);
        Engine.KeyboardEventPoller.StartMonitoringKey((int)Keys.D);
        Engine.KeyboardEventPoller.StartMonitoringKey((int)Keys.Left);
        Engine.KeyboardEventPoller.StartMonitoringKey((int)Keys.Right);
        Engine.KeyboardEventPoller.StartMonitoringKey((int)Keys.Up);
        Engine.KeyboardEventPoller.StartMonitoringKey((int)Keys.Down);
        Engine.KeyboardEventPoller.StartMonitoringKey((int)Keys.PageUp);
        Engine.KeyboardEventPoller.StartMonitoringKey((int)Keys.PageDown);
    }

    private void KeyboardEventPoller_KeyDown(KeyDownEventArgs args)
    {
        // Parse the received key string into the Keys enum (case-insensitive)
        if (!Enum.TryParse<Keys>(args.KeyConfig.Key, ignoreCase: true, out var key))
        {
            // If parsing fails, ignore — preserves existing behavior for any non-standard strings
            return;
        }

        switch (key)
        {
            case Keys.W:
                break;
            case Keys.A:
                break;
            case Keys.S:
                break;
            case Keys.D:
                break;
            case Keys.Right:
                break;
            case Keys.Left:
                break;
            case Keys.Up:
                break;
            case Keys.Down:
                break;
            case Keys.PageUp:
                break;
            case Keys.PageDown:
                break;
            default:
                break;
        }
    }

    private void ConfigureMouseInput()
    {
        Engine.Instance.InitializeWinFormsMouseAdapter(RenderSurface);
        Engine.MouseEventPoller!.MouseEvent += MouseEventPoller_MouseEvent;
        Engine.MouseEventPoller.StartMonitoringMouse();
    }

    private void MouseEventPoller_MouseEvent(Gondwana.Input.Mouse.MouseEventArgs args)
    {
        var view = RenderSurface.Host.ViewRenderer.Views[0];
        var layer = Scene.SceneLayers[0];

        var screenPos = args.CurrentPosition;

        // 1) screen → world (via View)
        var worldFromScreen = view.ScreenPxToWorldPx(layer, screenPos);

        // 2) world → grid (via View wrapper, which calls SceneLayer internally)
        var gridFromScreen = view.ScreenPxToGrid(layer, screenPos);

        // 3) grid → world (via SceneLayer wrapper)
        var worldFromGrid = layer.GridToWorldPx(gridFromScreen);

        // 4) world → screen (via View)
        var screenFromGrid = view.WorldPxToScreenPx(layer, worldFromGrid);
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
                Engine.Instance.State.SaveToFile("game.json");

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