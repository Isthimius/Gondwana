using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Direct.Particles;
using Gondwana.Input.Gamepad;
using Gondwana.Logging;
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
        RenderSurface.Host.Bind(Scene, false);
        RenderSurface.Host.Backbuffer!.FogPaint.Color = new SKColor(220, 230, 255, 120);

        RenderSurface.Host.ViewRenderer.AddView(new Rectangle(800, 0, 800, 900), 1f);
        RenderSurface.Host.ViewRenderer.Views[0].Camera.SnapTo(new PointF(-800, -100));
        RenderSurface.Host.ViewRenderer.Views[1].Camera.SnapTo(new PointF(100, 100));
        RenderSurface.Host.RedrawDirtyRectangleOnly = false;

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
        //Implementation for creating direct drawings goes here

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

        //InitializeParticles();
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
        var sceneLayer1 = scene.AddLayer(60, 5, 64, 64, 10, 1f, CoordinateSystemTypes.HexFlatTop);
        var sceneLayer2 = scene.AddLayer(60, 5, 32, 32, 5, 0.5f, CoordinateSystemTypes.HexFlatTop);

        sceneLayer1.ShowGridLines = true;
        sceneLayer2.ShowGridLines = true;

        return scene;
    }

    #region input configuration

    private void ConfigureKeyboardInput()
    {
        Engine.Instance.InitializeWinFormsKeyboardAdapter(RenderSurface);
        Engine.KeyboardEventPoller!.KeyDown += KeyboardEventPoller_KeyDown;
        Engine.KeyboardEventPoller.StartMonitoringKey(Keys.W.ToString());
        Engine.KeyboardEventPoller.StartMonitoringKey(Keys.A.ToString());
        Engine.KeyboardEventPoller.StartMonitoringKey(Keys.S.ToString());
        Engine.KeyboardEventPoller.StartMonitoringKey(Keys.D.ToString());
    }

    private void KeyboardEventPoller_KeyDown(Input.Keyboard.KeyDownEventArgs args)
    {
        var camera = RenderSurface.Host.ViewRenderer.Views[0].Camera;
        var curPos = camera.PositionPx;

        Engine.Logger.LogTrace("Key={Key} BEFORE pan: {X}, {Y}", args.KeyConfig.Key, curPos.X, curPos.Y);

        // Parse the received key string into the Keys enum (case-insensitive)
        if (!Enum.TryParse<Keys>(args.KeyConfig.Key, ignoreCase: true, out var key))
        {
            // If parsing fails, ignore — preserves existing behavior for any non-standard strings
            return;
        }

        switch (key)
        {
            case Keys.W:
                Engine.Logger.LogTrace("W key pressed");
                camera.PanTo(new PointF(curPos.X, curPos.Y - 10), 10f);
                break;
            case Keys.A:
                Engine.Logger.LogTrace("A key pressed");
                camera.PanTo(new PointF(curPos.X - 10, curPos.Y), 10f);
                break;
            case Keys.S:
                Engine.Logger.LogTrace("S key pressed");
                camera.PanTo(new PointF(curPos.X, curPos.Y + 10), 10f);
                break;
            case Keys.D:
                Engine.Logger.LogTrace("D key pressed");
                camera.PanTo(new PointF(curPos.X + 10, curPos.Y), 10f);
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

    private void MouseEventPoller_MouseEvent(Input.Mouse.MouseEventArgs args)
    {
        var view = RenderSurface.Host.ViewRenderer.Views[0];
        var layer = Scene!.SceneLayers[0];

        var screenPos = args.CurrentPosition;

        // 1) screen → world (via View)
        var worldFromScreen = view.ScreenPxToWorldPx(layer, screenPos);

        // 2) world → grid (via View wrapper, which calls SceneLayer internally)
        var gridFromScreen = view.ScreenPxToGrid(layer, screenPos);

        // 3) grid → world (via SceneLayer wrapper)
        var worldFromGrid = layer.GridToWorldPx(gridFromScreen);

        // 4) world → screen (via View)
        var screenFromGrid = view.WorldPxToScreenPx(layer, worldFromGrid);

        var dx = screenFromGrid.X - screenPos.X;
        var dy = screenFromGrid.Y - screenPos.Y;

        Engine.Logger.LogTrace(
            "ROUNDTRIP: ΔSCR = ({0:F3}, {1:F3})  " +
            "SCR={2:F1},{3:F1}  SCR(grid)={4:F1},{5:F1}",
            dx, dy,
            screenPos.X, screenPos.Y,
            screenFromGrid.X, screenFromGrid.Y
        );

        Engine.Logger.LogTrace(
            "PICK DEBUG: " +
            "SCR={0,6:F1},{1,6:F1}  " +
            "W(scr)={2,7:F1},{3,7:F1}  " +
            "GRID={4,5:F2},{5,5:F2}  " +
            "W(grid)={6,7:F1},{7,7:F1}  " +
            "SCR(grid)={8,6:F1},{9,6:F1}",
            screenPos.X, screenPos.Y,
            worldFromScreen.X, worldFromScreen.Y,
            gridFromScreen.X, gridFromScreen.Y,
            worldFromGrid.X, worldFromGrid.Y,
            screenFromGrid.X, screenFromGrid.Y
        );

        // Existing HUD text
        var cameraPos = view.Camera.PositionPx;
        var message =
            $"Mouse Pos (screen): {screenPos.X}, {screenPos.Y}\n" +
            $"World Pos (px): {worldFromScreen.X:F1}, {worldFromScreen.Y:F1}\n" +
            $"Grid coordinates: {gridFromScreen.X}, {gridFromScreen.Y}\n" +
            $"Camera Pos: (px): {cameraPos.X}, {cameraPos.Y}";
        _textBlockMouse?.SetText(message);

        // Highlight logic, unchanged
        foreach (SceneLayerTile tile in layer)
            tile.EnableFog = false;

        var pickedTile = layer[gridFromScreen];
        if (pickedTile is not null)
            pickedTile.EnableFog = true;

        // Zoom with scroll, unchanged
        if (args.ScrollDelta != 0)
            view.Viewport.Zoom += args.ScrollDelta * 0.001f;

        if (args.ButtonStates[Input.Mouse.MouseButton.Left].IsDown)
        {
            var pos = args.CurrentPosition;
            //_clickEmitter.Position = new PointF(pos.X, pos.Y);
            //_particleSurface.Burst(_clickEmitter, 80);
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
