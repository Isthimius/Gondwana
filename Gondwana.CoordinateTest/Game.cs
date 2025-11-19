using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Gamepad;
using Gondwana.Logging;
using Gondwana.Scenes;
using Gondwana.WinForms;
using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;
using SkiaSharp;

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
        RenderSurface.Host.Backbuffer!.FogPaint.Color = new SKColor(220, 230, 255, 120);

        // configure input handling here
        ConfigureKeyboardInput();
        ConfigureMouseInput();
        ConfigureGamepadInput();

        // start the engine main loop
        Engine.Instance.Start(SynchronizationContext.Current!);

        RenderSurface.Host.ViewRenderer.AddView(new Rectangle(800, 0, 800, 900), 1f);
        RenderSurface.Host.ViewRenderer.Views[0].Camera.SnapTo(new PointF(-800, -100));
        RenderSurface.Host.ViewRenderer.Views[1].Camera.SnapTo(new PointF(100, 100));
        //RenderSurface.Host.RedrawDirtyRectangleOnly = false;

        RenderSurface.Host.Scene[0].OriginPx = new Point(100, 100);
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

        var screenPos = args.CurrentPosition;
        var worldPos = view.ScreenPxToWorldPx(screenPos);           // uses camera + viewport
        var gridPos = view.ScreenPxToGrid(layer, screenPos);        // uses worldPos internally

        var message =
            $"Mouse Pos (screen): {screenPos.X}, {screenPos.Y}\n" +
            $"World Pos (px): {worldPos.X:F1}, {worldPos.Y:F1}\n" +
            $"Grid coordinates: {gridPos.X}, {gridPos.Y}";

        _textBlockMouse?.SetText(message);

        foreach (SceneLayerTile tile in layer)
            tile.EnableFog = false;

        if (layer[gridPos] is not null)
            layer[gridPos]!.EnableFog = true;
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
}
