using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Direct.Particles;
using Gondwana.Input.Gamepad;
using Gondwana.Logging;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Gondwana.WinForms;
using Gondwana.WinForms.Rendering;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Drawing;
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
        RenderSurface.Host.Backbuffer!.FogPaint.Color = new SKColor(220, 230, 255, 120);

        RenderSurface.Host.ViewRenderer.AddView(new Rectangle(800, 0, 800, 900), 1f);
        RenderSurface.Host.ViewRenderer.Views[0].Camera.SnapTo(new PointF(-800, -100));
        RenderSurface.Host.ViewRenderer.Views[1].Camera.SnapTo(new PointF(100, 100));
        //RenderSurface.Host.RedrawDirtyRectangleOnly = false;

        RenderSurface.Host.Scene[0].OriginPx = new Point(100, 100);

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
    private ParticleSurface? _particleSurface;
    private ParticleEmitter? _clickEmitter;

    private void InitDirectDrawings()
    {
        // Implementation for creating direct drawings goes here
        _directRectangle = new DirectRectangle(RenderSurface.Host,
                                               new Rectangle(RenderSurface.Size.Width - 250, 0, 250, 150),
                                               Color.Wheat);
        _directRectangle.SetFilled(true);

        _textBlockCPS = new TextBlock(RenderSurface.Host, _directRectangle.Bounds);
        _textBlockCPS.SetColors(Color.Black, Color.Transparent).ZOrder = 10;

        Engine.Instance.CPSCalculated += (e) =>
        {
            _textBlockCPS.SetText(e.ToString());
        };

        _textBlockMouse = new TextBlock(RenderSurface.Host, new Rectangle(RenderSurface.Size.Width - 250, 200, 250, 150));
        _textBlockMouse.SetColors(Color.Black, Color.Wheat).ZOrder = 10;

        InitializeParticles();
    }

    private void InitializeParticles()
    {
        // Cover the whole adapter in pixels
        var bounds = new Rectangle(
            0,
            0,
            RenderSurface.Host.RenderSurfaceAdapter!.Width,
            RenderSurface.Host.RenderSurfaceAdapter!.Height);

        // Particle system registered like any other DirectDrawing
        _particleSurface = new ParticleSurface(RenderSurface.Host, bounds);

        // Tweak gravity if you want more “floaty” bursts
        _particleSurface.GravityY = 0f;

        // Configure an emitter specifically for click bursts
        _clickEmitter = new ParticleEmitter
        {
            EmitRate = 0f, // we only use Burst(), no continuous emission

            LifeRange = (0.35f, 0.7f),
            VelocityRangeX = (-280f, 280f),
            VelocityRangeY = (-280f, 280f),
            SizeRange = (2f, 5f),

            Color = SKColors.OrangeRed,
            MaxVelocity = 400f,   // optional, keeps them from going insane
        };
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
        Engine.KeyboardEventPoller.StartMonitoringKey("W");
        Engine.KeyboardEventPoller.StartMonitoringKey("A");
        Engine.KeyboardEventPoller.StartMonitoringKey("S");
        Engine.KeyboardEventPoller.StartMonitoringKey("D");
    }

    private void KeyboardEventPoller_KeyDown(Input.Keyboard.KeyDownEventArgs args)
    {
        var camera = RenderSurface.Host.ViewRenderer.Views[0].Camera;
        var curPos = camera.PositionPx;

        switch (args.KeyConfig.Key)
        {
            case "W":
                Engine.Logger.LogTrace("W key pressed");
                camera.PanTo(new PointF(curPos.X, curPos.Y - 10), 10f);
                break;
            case "A":
                Engine.Logger.LogTrace("A key pressed");
                camera.PanTo(new PointF(curPos.X - 10, curPos.Y), 10f);
                break;
            case "S":
                Engine.Logger.LogTrace("S key pressed");
                camera.PanTo(new PointF(curPos.X, curPos.Y + 10), 10f);
                break;
            case "D":
                Engine.Logger.LogTrace("D key pressed");
                camera.PanTo(new PointF(curPos.X + 10, curPos.Y), 10f);
                break;
            default:
                break;
        }

        // Handle key down events here
        //if (args.KeyConfig.Key == "W")
        //{
        //    RenderSurface.Host.ViewRenderer.Views[0].Camera.PanTo(new PointF(0, -20), 0.1f);
        //}
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

        if (args.ScrollDelta != 0)
            view.Viewport.Zoom += args.ScrollDelta * 0.001f;

        if (args.ButtonStates[Input.Mouse.MouseButton.Left].IsDown)
        {
            var pos = args.CurrentPosition;
            _clickEmitter.Position = new PointF(pos.X, pos.Y);
            _particleSurface.Burst(_clickEmitter, 80);
        }
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
