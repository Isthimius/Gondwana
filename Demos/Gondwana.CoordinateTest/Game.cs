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
using Gondwana.Drawing.Tilesheets;
using Gondwana.Drawing.Sprites;
using System.Numerics;
using Gondwana.Drawing.Animation;

namespace Gondwana.Demos.CoordinateTest;

public class Game : IDisposable
{
    public WinFormGpuRenderSurfaceControl RenderSurface { get; private set; }

    public Scene Scene { get; private set; }

    public Game(WinFormGpuRenderSurfaceControl renderSurface)
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
        RenderSurface.Host.Bind(Scene, false);
        RenderSurface.Host.Backbuffer!.FogPaint.Color = new SKColor(220, 230, 255, 120);

        RenderSurface.Host.ViewManager.AddView(new Rectangle(800, 0, 800, 300), 1f, 10);
        RenderSurface.Host.ViewManager.Views[0].Camera.SnapTo(new PointF(-800, -100));
        RenderSurface.Host.ViewManager.Views[1].Camera.SnapTo(new PointF(100, 100));
        RenderSurface.Host.RedrawDirtyRectangleOnly = true;

        RenderSurface.Host.Scene[0].OriginPx = new Point(-100, -100);

        InitSprites();
        InitDirectDrawings();

        RenderSurface.Host.ViewManager.Views[0].Camera.FollowCentered(SpriteManager.Instance.GetSpriteByID("rooster_1")!);

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
        var tilesheet = new Tilesheet("rooster", "assets/rooster.bmp");
        tilesheet.DefaultRegion.TileSize = new Size(50, 50);
        tilesheet.ApplyMask(SKColors.Black, 60);

        var tilesheet2 = new Tilesheet("tiles", "assets/original.bmp");
        tilesheet2.DefaultRegion.TileSize = new Size(64, 32);
        tilesheet2.DefaultRegion.Area = new Rectangle(1, 1, tilesheet2.SkBitmap.Width - 2, tilesheet2.SkBitmap.Height - 2);
        //tilesheet2.DefaultRegion.TilePadding = new Size(1, 1);
    }

    private void LoadAnimationCycles()
    {
        // Implementation for loading animation cycles goes here
    }

    private void InitSprites()
    {
        // Implementation for creating sprites goes here
        var tilesheet = TilesheetRegistry.Instance.GetAll()["rooster"];
        
        var sprite1 = SpriteManager.Instance.CreateSprite(Scene[0], tilesheet[0, 0], "rooster_1");
        sprite1.Visible = true;
        sprite1.CollisionsEnabled = true;

        var sprite2 = SpriteManager.Instance.CreateSprite(Scene[0], tilesheet[0, 0], "rooster_2");
        sprite2.Visible = true;
        sprite2.SetPosition(new Vector2(5, 0));
        sprite2.CollisionsEnabled = true;

        FrameSequence frameSequence = new FrameSequence();
        frameSequence.AddFrame(tilesheet, 0, 0);
        frameSequence.AddFrame(tilesheet, 1, 0);
        frameSequence.AddFrame(tilesheet, 2, 0);
        frameSequence.AddFrame(tilesheet, 3, 0);
        frameSequence.SequenceCycleType = CycleType.PingPong;
        sprite1.TileAnimator.CurrentCycle = new Cycle(frameSequence, 0.5f, "ani");
        sprite1.TileAnimator.StartAnimation();
    }

    private DirectRectangle? _directRectangle;
    private TextBlock? _textBlockCPS;
    private TextBlock? _textBlockMouse;
    private ParticleSurface? _particleSurface;
    private ParticleEmitter? _clickEmitter;

    private TextBlock? _spriteNameTag;

    private void InitDirectDrawings()
    {
        //Implementation for creating direct drawings goes here

        var bounds1 = new Rectangle(RenderSurface.Size.Width - 250, 0, 250, 150);
        var bounds2 = new Rectangle(RenderSurface.Size.Width - 250, 200, 250, 150);

        _directRectangle = new DirectRectangle(Color.Wheat,
                                               RenderSurface.Host,
                                               RenderSurface.Host.ViewManager.Views[0],
                                               bounds1,
                                               null);
        _directRectangle.SetFilled(true).SetAlpha(128);

        _textBlockCPS = new TextBlock(RenderSurface.Host,
                                      RenderSurface.Host.ViewManager.Views[0],
                                      bounds1,
                                      null);
        _textBlockCPS.SetColors(Color.Black, Color.Transparent).ZOrder = 10;

        Engine.Instance.CPSCalculated += (e) =>
        {
            _textBlockCPS.SetText(e.ToString());
        };

        _textBlockMouse = new TextBlock(RenderSurface.Host,
                                        RenderSurface.Host.ViewManager.Views[0],
                                        bounds2,
                                        null);
        _textBlockMouse.SetColors(Color.Black, Color.Wheat).ZOrder = 10;

        InitializeParticles();

        _spriteNameTag = new TextBlock(RenderSurface.Host,
                                                       Scene[0],
                                                       null,
                                                       new Rectangle(0, 0, 150, 30));
        _spriteNameTag.SetColors(Color.Blue, Color.White).SetText("Mister Rooster").ZOrder = 20;
        _spriteNameTag.Movement.FollowTileSoft(SpriteManager.Instance.GetSpriteByID("rooster_1")!, 0.75f, 0.1f, new Vector2(0, 0.75f));
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
        _particleSurface = new ParticleSurface(RenderSurface.Host,
                                               RenderSurface.Host.ViewManager.Views[0],
                                               bounds,
                                               null);

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

            JitterX = 40,
            JitterY = 40,

            // Perfect round “boom”
            //SpawnDistribution = ParticleSpawnDistribution.Ellipse,

            // Shockwave ring
            //SpawnDistribution = ParticleSpawnDistribution.Ring,
            //RingInnerRadius01 = 0.92f,

            // Smoky / magical puff
            SpawnDistribution = ParticleSpawnDistribution.Gaussian,
            GaussianStdDev01 = 0.45f
        };

        //_particleSurface.Emitters.Add(GetSmoke(bounds.Width, bounds.Height));
    }

    private ParticleEmitter GetSmoke(float width, float height)
    {
        return new ParticleEmitter
        {
            Position = new PointF(width / 2, height),
            EmitRate = 120,
            LifeRange = (2.5f, 4.0f),
            VelocityRangeX = (-40f, 40f),
            VelocityRangeY = (-120f, -60f),
            SizeRange = (8f, 16f),
            Color = new SKColor(80, 80, 80, 200),
            GravityY = -20f // slight upward drift
        };
    }

    #endregion load and init game content

    private Scene? CreateInitialScene()
    {
        var scene = new Scene();
        var sceneLayer1 = scene.AddLayer(60, 5, 64, 64, 10, 1f, CoordinateSystemTypes.Orthogonal);
        var sceneLayer2 = scene.AddLayer(60, 5, 32, 32, 5, 0.5f, CoordinateSystemTypes.Orthogonal);

        sceneLayer1.ShowGridLines = true;
        sceneLayer1.ShowCollisionBoxes = false;
        sceneLayer2.ShowGridLines = true;

        var sourceTilesheet = TilesheetRegistry.Instance.GetAll()["tiles"];
        sceneLayer1[0, 0].CurrentFrame = sourceTilesheet[4, 4];
        sceneLayer1[1, 0].CurrentFrame = sourceTilesheet[4, 4];
        sceneLayer1[2, 0].CurrentFrame = sourceTilesheet[4, 4];
        sceneLayer1[0, 1].CurrentFrame = sourceTilesheet[4, 4];
        sceneLayer1[1, 1].CurrentFrame = sourceTilesheet[4, 4];
        sceneLayer1[2, 1].CurrentFrame = sourceTilesheet[4, 4];
        sceneLayer1[0, 2].CurrentFrame = sourceTilesheet[4, 4];
        sceneLayer1[1, 2].CurrentFrame = sourceTilesheet[4, 4];
        sceneLayer1[2, 2].CurrentFrame = sourceTilesheet[4, 4];

        sceneLayer2[0, 0].CurrentFrame = sourceTilesheet[3, 3];
        sceneLayer2[1, 0].CurrentFrame = sourceTilesheet[3, 3];
        sceneLayer2[2, 0].CurrentFrame = sourceTilesheet[3, 3];
        sceneLayer2[0, 1].CurrentFrame = sourceTilesheet[4, 3];
        sceneLayer2[1, 1].CurrentFrame = sourceTilesheet[3, 3];
        sceneLayer2[2, 1].CurrentFrame = sourceTilesheet[3, 3];
        sceneLayer2[0, 2].CurrentFrame = sourceTilesheet[3, 3];
        sceneLayer2[1, 2].CurrentFrame = sourceTilesheet[3, 3];
        sceneLayer2[2, 2].CurrentFrame = sourceTilesheet[3, 3];
        sceneLayer2[10, 0].CurrentFrame = sourceTilesheet[3, 3];
        sceneLayer2[11, 0].CurrentFrame = sourceTilesheet[3, 3];
        sceneLayer2[12, 0].CurrentFrame = sourceTilesheet[3, 3];
        sceneLayer2[10, 1].CurrentFrame = sourceTilesheet[4, 3];
        sceneLayer2[11, 1].CurrentFrame = sourceTilesheet[3, 3];
        sceneLayer2[12, 1].CurrentFrame = sourceTilesheet[3, 3];
        sceneLayer2[10, 2].CurrentFrame = sourceTilesheet[3, 3];
        sceneLayer2[11, 2].CurrentFrame = sourceTilesheet[3, 3];
        sceneLayer2[12, 2].CurrentFrame = sourceTilesheet[3, 3];

        return scene;
    }

    #region input configuration

    private void ConfigureKeyboardInput()
    {
        Engine.Instance.InitializeWinFormsKeyboardAdapter(RenderSurface);
        Engine.Instance.Input.KeyboardEventPoller!.KeyDown += KeyboardEventPoller_KeyDown;
        Engine.Instance.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.W, "W");
        Engine.Instance.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.A, "A");
        Engine.Instance.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.S, "S");
        Engine.Instance.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.D, "D");
        Engine.Instance.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.Left, "Left");
        Engine.Instance.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.Right, "Right");
        Engine.Instance.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.Up, "Up");
        Engine.Instance.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.Down, "Down");
        Engine.Instance.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.PageUp, "PageUp");
        Engine.Instance.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.PageDown, "PageDown");
    }

    private void KeyboardEventPoller_KeyDown(Input.Keyboard.KeyDownEventArgs args)
    {
        var camera = RenderSurface.Host.ViewManager.Views[0].Camera;
        var curPos = camera.PositionPx;
        var sprite = SpriteManager.Instance.GetSpriteByID("rooster_1");

        // Parse the received key string into the Keys enum (case-insensitive)
        if (!Enum.TryParse<Keys>(args.KeyConfig.Key, ignoreCase: true, out var key))
        {
            // If parsing fails, ignore — preserves existing behavior for any non-standard strings
            return;
        }

        switch (key)
        {
            case Keys.W:
                camera.PanToOverDuration(new PointF(curPos.X, curPos.Y - 100), 1.5f);
                break;
            case Keys.A:
                camera.PanToOverDuration(new PointF(curPos.X - 100, curPos.Y), 1.5f);
                break;
            case Keys.S:
                camera.PanToOverDuration(new PointF(curPos.X, curPos.Y + 100), 1.5f);
                break;
            case Keys.D:
                camera.PanToOverDuration(new PointF(curPos.X + 100, curPos.Y), 1.5f);
                break;
            case Keys.Right:
                if (args.KeyAction == Input.Keyboard.KeyAction.Released)
                    sprite.Movement.SetAcceleration(new Vector2(0, 0));
                else
                    sprite.Movement.SetAcceleration(new Vector2(2f, 0));

                sprite.Movement.SetLinearDamping(0.3f);

                break;
            case Keys.Left:
                if (args.KeyAction == Input.Keyboard.KeyAction.Released)
                    sprite.Movement.SetAcceleration(new Vector2(0, 0));
                else
                    sprite.Movement.SetAcceleration(new Vector2(-2f, 0));

                break;
            case Keys.Up:
                if (args.KeyAction == Input.Keyboard.KeyAction.Released)
                    sprite.Movement.SetAcceleration(new Vector2(0, 0));
                else
                    sprite.Movement.SetAcceleration(new Vector2(0, -2f));

                sprite.Movement.SetLinearDamping(0.3f);

                break;
            case Keys.Down:
                if (args.KeyAction == Input.Keyboard.KeyAction.Released)
                    sprite.Movement.SetAcceleration(new Vector2(0, 0));
                else
                    sprite.Movement.SetAcceleration(new Vector2(0, 2f));

                break;
            case Keys.PageUp:
                sprite.ScaleBy(1.1f, 0.15f);
                break;
            case Keys.PageDown:
                sprite.ScaleBy(0.9f, 0.15f);
                break;
            default:
                break;
        }
    }

    private void ConfigureMouseInput()
    {
        Engine.Instance.InitializeWinFormsMouseAdapter(RenderSurface);
        Engine.Instance.Input.MouseEventPoller!.MouseEvent += MouseEventPoller_MouseEvent;
        Engine.Instance.Input.MouseEventPoller.StartMonitoringMouse();
    }

    private void MouseEventPoller_MouseEvent(Input.Mouse.MouseEventArgs args)
    {
        var view = RenderSurface.Host.ViewManager.Views[0];
        var layer = Scene.SceneLayers[0];

        var screenPos = args.CurrentPosition;

        var worldPx = view.ScreenPxToWorldPx(layer, screenPos);
        var screenPx = view.WorldPxToScreenPx(layer, worldPx);
        //Engine.Logger.LogTrace($"mouse={screenPos} roundtrip={s} cam={view.Camera.PositionPx} zoom={view.Viewport.Zoom} p={layer.Parallax}");
        //Engine.Logger.LogTrace($"\r\nscreen1 = {screenPos} \r\nworld   = {worldPx} \r\nscreen2 = {screenPx}\r\n");

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

        ScrollWheelZoom(args, view, layer);

        if (args.ButtonStates[Input.Mouse.MouseButton.Left].JustPressed)
        {
            var pos = args.CurrentPosition;
            _clickEmitter.Position = new PointF(pos.X, pos.Y);
            _particleSurface.Burst(_clickEmitter, 80);
        }
    }

    private void ScrollWheelZoom(Input.Mouse.MouseEventArgs args, Rendering.Views.View view, SceneLayer layer)
    {
        // Zoom with scroll, unchanged
        //if (args.ScrollDelta != 0)
        //    view.Viewport.Zoom += args.ScrollDelta * 0.001f;

        // smooth zoom
        //if (args.ScrollDelta != 0)
        //{
        //    var vp = view.Viewport;
        //    float currentZoom = vp.Zoom;
        //    float delta = args.ScrollDelta * 0.001f;

        //    float minZoom = 0.1f;
        //    float maxZoom = 8f;

        //    float targetZoom = Math.Clamp(currentZoom + delta, minZoom, maxZoom);

        //    // ~0.15s feels nice; tweak as you like
        //    vp.ZoomToOverDuration(targetZoom, 0.35f);
        //}

        //// smooth zoom around mouse position
        if (args.ScrollDelta != 0)
        {
            var vp = view.Viewport;
            float currentZoom = vp.Zoom;
            float delta = args.ScrollDelta * 0.001f;

            float minZoom = 0.1f;
            float maxZoom = 8f;

            float targetZoom = Math.Clamp(currentZoom + delta, minZoom, maxZoom);

            // 0.15s – tweak to taste
            view.ZoomAroundScreenPoint(layer, args.CurrentPosition, targetZoom, 0.75f);
        }
    }

    private void ConfigureGamepadInput()
    {
        //Engine.Instance.InitializeSdlGamepadManager();

        Engine.Instance.InitializeXInputGamepadManager();
        Engine.Instance.Input.GamepadEventPoller!.ButtonDown += GamepadEventPoller_ButtonDown;

        foreach (var gamepadAdapter in Engine.Instance.Input.GamepadManager!.ConnectedAdapters)
        {
            Engine.Instance.Input.GamepadEventPoller.StartMonitoringButton(gamepadAdapter.GamepadId, "");
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

                Engine.Instance.Input.KeyboardEventPoller!.KeyDown -= KeyboardEventPoller_KeyDown;
                Engine.Instance.Input.MouseEventPoller!.MouseEvent -= MouseEventPoller_MouseEvent;
                Engine.Instance.Input.GamepadEventPoller!.ButtonDown -= GamepadEventPoller_ButtonDown;

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
