using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Avalonia.Input;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Gondwana;
using Gondwana.Audio;
using Gondwana.Avalonia.Hosting;
using Gondwana.Avalonia.Rendering;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Direct.Particles;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using Gondwana.Timers;
using Gondwana.Demos.SpotAvalonia.Game;
#if BROWSER
using Gondwana.Audio.Browser;
#endif

namespace Gondwana.Demos.SpotAvalonia;

/// <summary>
/// Game host for SpotAvalonia. Uses <see cref="AvaloniaGameHost"/> with the
/// <see cref="AvaloniaBitmapRenderSurfaceControl"/> renderer so the game runs on all
/// Avalonia targets — including browser/WASM via the timer-driven engine loop.
/// </summary>
internal sealed class SpotAvaloniaGameHost : AvaloniaGameHost
{
    private bool _initialGameStarted;
    private bool _handleHumanInput = false;
    private bool _showScores = true;

    private ParticleSurface? _particleSurface;

    internal TextBlock? _player1Text;
    internal DirectRectangle? _player1Rectangle;
    internal TextBlock? _player2Text;
    internal DirectRectangle? _player2Rectangle;
    internal TextBlock? _player3Text;
    internal DirectRectangle? _player3Rectangle;
    internal TextBlock? _player4Text;
    internal DirectRectangle? _player4Rectangle;
    internal TextBlock? _gameMessageText;
    internal DirectRectangle? _gameMessageRectangle;

    // Audio is skipped on browser targets where it is unsupported.
    internal AudioResource? _music;
    internal AudioResource? _velcro;
    internal AudioResource? _drop;
    internal AudioResource? _gameWin;
    internal AudioResource? _gameLose;
    internal AudioResource? _bump;
    internal AudioResource? _knock;
    internal AudioResource? _spotSelected;
    internal AudioResource? _spotDeselected;

#if BROWSER
    // Browser/WASM audio (HTML5 Audio API via BrowserAudioManager).
    private BrowserAudioPlayer? _browserMusic;
    private BrowserAudioPlayer? _browserVelcro;
    private BrowserAudioPlayer? _browserDrop;
    private BrowserAudioPlayer? _browserGameWin;
    private BrowserAudioPlayer? _browserGameLose;
    private BrowserAudioPlayer? _browserBump;
#endif

    internal Tilesheet _blueSpot = null!;
    internal Tilesheet _greenSpot = null!;
    internal Tilesheet _pinkSpot = null!;
    internal Tilesheet _redSpot = null!;
    internal Tilesheet _yellowSpot = null!;
    internal Tilesheet _blueSpotHappy = null!;
    internal Tilesheet _greenSpotHappy = null!;
    internal Tilesheet _pinkSpotHappy = null!;
    internal Tilesheet _redSpotHappy = null!;
    internal Tilesheet _yellowSpotHappy = null!;
    internal Tilesheet _clouds = null!;

    internal SKTypeface _font = null!;

    internal SpotGame SpotGame { get; private set; } = null!;

    private Gondwana.Timers.Timer? _pendingComputerSelectTimer;
    private Gondwana.Timers.Timer? _pendingComputerMoveTimer;

    private static readonly Random _rng = new();

    internal Action? RequestNewGameDialog { get; set; }

    internal SpotAvaloniaGameHost(AvaloniaBitmapRenderSurfaceControl renderSurface)
        : base(renderSurface)
    {
    }

    #region AvaloniaGameHost overrides

    protected override void LoadAssets()
    {
#if BROWSER
        // Browser/WASM: use BrowserAudioManager (HTML5 Audio API via JS interop).
        // Audio paths use forward slashes and are relative to AppBundle/index.html.
        var browserAudio = Engine.GetBrowserAudioManager();
        _browserMusic    = browserAudio.Load("music",    "assets/sounovamusic-puzzle-amp-casual-game-music-460543.mp3",             volume: 0.2f, loop: true);
        _browserVelcro   = browserAudio.Load("velcro",   "assets/freesound_community-velcro_fast-91558.mp3");
        _browserDrop     = browserAudio.Load("drop",     "assets/freesound_community-water-drip-45622.mp3");
        _browserGameWin  = browserAudio.Load("gameWin",  "assets/peekaboolabcreative-11l-victory_sound_with_t-1749487402950-357606.mp3");
        _browserGameLose = browserAudio.Load("gameLose", "assets/freesound_community-080047_lose_funny_retro_video-game-80925.mp3");
        _browserBump     = browserAudio.Load("bump",     "assets/freesound_community-bump-7-92964.mp3");
#else
        // Desktop: use the NAudio-based AudioResourceManager.
        _music = Engine.Managers.AudioResources.LoadFromFile("music", "assets/sounovamusic-puzzle-amp-casual-game-music-460543.mp3");
        _music.IsLooping = true;

        _velcro   = Engine.Managers.AudioResources.LoadFromFile("velcro",   "assets/freesound_community-velcro_fast-91558.mp3");
        _drop     = Engine.Managers.AudioResources.LoadFromFile("drop",     "assets/freesound_community-water-drip-45622.mp3");
        _gameWin  = Engine.Managers.AudioResources.LoadFromFile("gameWin",  "assets/peekaboolabcreative-11l-victory_sound_with_t-1749487402950-357606.mp3");
        _gameLose = Engine.Managers.AudioResources.LoadFromFile("gameLose", "assets/freesound_community-080047_lose_funny_retro_video-game-80925.mp3");
        _bump     = Engine.Managers.AudioResources.LoadFromFile("bump",     "assets/freesound_community-bump-7-92964.mp3");

        _spotSelected = Engine.Managers.AudioResources.LoadFromFile("spotSelected", "assets/universfield-bubble-pop-293342.mp3");
        _spotSelected.Volume = 0.4f;
        _spotDeselected = Engine.Managers.AudioResources.LoadFromFile("spotDeselected", "assets/universfield-bubble-pop-293342.mp3");
        _spotDeselected.Volume = 0.15f;
        _knock = Engine.Managers.AudioResources.LoadFromFile("knock", "assets/rohhsadotcom-knock-on-wood-02-421991.mp3");
#endif

        _font = Engine.Managers.Fonts.LoadFromFile("main", "assets/ArchitectsDaughter-Regular.ttf");
    }

    protected override void LoadTilesheets()
    {
        // splash logo
        var splash = TilesheetRegistry.Instance.LoadFromImageFile("splash", "assets/spot.png");
        splash.ApplyMask(Color.Black.ToSKColor());

        // default sprites
        _blueSpot = TilesheetRegistry.Instance.LoadFromImageFile("blueSpot", "assets/bubble-blue.png");
        _blueSpot.DefaultRegion.TileSize = new Size(92, 96);

        _greenSpot = TilesheetRegistry.Instance.LoadFromImageFile("greenSpot", "assets/bubble-green.png");
        _greenSpot.DefaultRegion.TileSize = new Size(92, 96);

        _pinkSpot = TilesheetRegistry.Instance.LoadFromImageFile("pinkSpot", "assets/bubble-pink.png");
        _pinkSpot.DefaultRegion.TileSize = new Size(92, 96);

        _redSpot = TilesheetRegistry.Instance.LoadFromImageFile("redSpot", "assets/bubble-red.png");
        _redSpot.DefaultRegion.TileSize = new Size(92, 96);

        _yellowSpot = TilesheetRegistry.Instance.LoadFromImageFile("yellowSpot", "assets/bubble-yellow.png");
        _yellowSpot.DefaultRegion.TileSize = new Size(92, 96);

        // selected sprites
        _blueSpotHappy = TilesheetRegistry.Instance.LoadFromImageFile("blueSpotHappy", "assets/bubble-blue-happy.png");
        _blueSpotHappy.DefaultRegion.TileSize = new Size(64, 64);

        _greenSpotHappy = TilesheetRegistry.Instance.LoadFromImageFile("greenSpotHappy", "assets/bubble-green-happy.png");
        _greenSpotHappy.DefaultRegion.TileSize = new Size(64, 64);

        _pinkSpotHappy = TilesheetRegistry.Instance.LoadFromImageFile("pinkSpotHappy", "assets/bubble-pink-happy.png");
        _pinkSpotHappy.DefaultRegion.TileSize = new Size(64, 64);

        _redSpotHappy = TilesheetRegistry.Instance.LoadFromImageFile("redSpotHappy", "assets/bubble-red-happy.png");
        _redSpotHappy.DefaultRegion.TileSize = new Size(64, 64);

        _yellowSpotHappy = TilesheetRegistry.Instance.LoadFromImageFile("yellowSpotHappy", "assets/bubble-yellow-happy.png");
        _yellowSpotHappy.DefaultRegion.TileSize = new Size(64, 64);

        _clouds = TilesheetRegistry.Instance.LoadFromImageFile("clouds", "assets/clouds.png");
    }

    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();

        var sceneLayer1 = scene.AddLayer(
            columnCount: 1,
            rowCount: 1,
            width: 768,
            height: 768,
            zOrder: 10,
            parallax: 1f,
            coordinateSystem: CoordinateSystemTypes.Orthogonal);

        sceneLayer1.ShowGridLines = false;

        return scene;
    }

    protected override void CreateSceneGraph()
    {
        base.CreateSceneGraph();
        RenderSurface.Host.Backbuffer.ClearColor = Color.CornflowerBlue.ToSKColor();

        SpotGame = new SpotGame();
        HookSpotGameEvents();
    }

    protected override void CreateDirectDrawings()
    {
        // Startup presentation (splash logo + particle surface) is deferred to
        // BeginPostSplashStartup(), which is called after InitializeAsync() completes.
    }

    protected override void OnStartEngine()
    {
        // Music and startup visuals begin in BeginPostSplashStartup() after the splash.
    }

#if !BROWSER
    protected override Gondwana.Hosting.SplashScreen? CreateSplash(Gondwana.Rendering.RenderSurfaceHostBase host)
    {
        var imagePath = System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "gondwana-logo-text.png");
        var splash = Gondwana.Hosting.SplashScreen.TryCreate(host, imagePath);
        if (splash != null)
            splash.HoldSec = 3f;
        return splash;
    }
#endif

    /// <summary>
    /// Creates the startup presentation (spot particles, splash logo, and music) that is shown
    /// after the splash screen completes. Called by <see cref="GameWindow"/> on desktop targets
    /// and by <see cref="GameView"/> on browser/WASM targets immediately after initialization.
    /// </summary>
    internal void BeginPostSplashStartup()
    {
        Tilesheet tilesheet;

        if (TilesheetRegistry.Instance.TryGet("splash", out tilesheet))
        {
            var directImage = new DirectImage(
                tilesheet.SkBitmap,
                RenderSurface.Host,
                Scene[0],
                new Rectangle(0, 0, 769, 769));

            directImage.ZOrder = 100;
            directImage.SetScaleMode(DirectImage.ScaleMode.Fit);
        }

        var particleSurface = new ParticleSurface(
            RenderSurface.Host,
            Scene[0],
            new Rectangle(0, 0, 769, 769));

        particleSurface.CullingMarginX = 1300f;
        particleSurface.ZOrder = 50;
        particleSurface.Emitters.Add(GetSpots(769, 769));

        if (MusicEnabled)
        {
#if BROWSER
            if (_browserMusic != null)
            {
                _browserMusic.Volume = 0.2f;
                _browserMusic.Play();
            }
#else
            if (_music != null)
            {
                _music.Volume = 0.2f;
                _music.Play();
            }
#endif
        }
    }

    protected override void OnMouseAdapterInitialized()
    {
        if (Engine.Input.MouseEventPoller is null)
            return;

        Engine.Input.MouseEventPoller.MouseEvent += MouseEventPoller_MouseEvent;
        Engine.Input.MouseEventPoller.StartMonitoringMouse();
    }

    protected override void OnKeyboardAdapterInitialized()
    {
        if (Engine.Input.KeyboardEventPoller is null)
            return;

        Engine.Input.KeyboardEventPoller.KeyDown += KeyboardEventPoller_KeyDown;
        Engine.Input.KeyboardEventPoller.StartMonitoringKey((int)Key.S);
    }

    protected override void UnhookEvents()
    {
        if (Engine.Input.MouseEventPoller is not null)
            Engine.Input.MouseEventPoller.MouseEvent -= MouseEventPoller_MouseEvent;

        if (Engine.Input.KeyboardEventPoller is not null)
            Engine.Input.KeyboardEventPoller.KeyDown -= KeyboardEventPoller_KeyDown;

        UnhookSpotGameEvents();
    }

    #endregion AvaloniaGameHost overrides

    #region game settings

    internal bool MusicEnabled { get; private set; } = true;

    internal void SetMusicEnabled(bool enabled)
    {
        MusicEnabled = enabled;

        if (enabled)
        {
#if BROWSER
            _browserMusic?.Play(fromStart: false);
#else
            _music?.Play();
#endif
        }
        else
        {
#if BROWSER
            _browserMusic?.Stop();
#else
            _music?.Stop();
#endif
        }
    }

    internal bool SoundEffectsEnabled { get; private set; } = true;

    internal void SetSoundEffectsEnabled(bool enabled)
    {
        SoundEffectsEnabled = enabled;
    }

    internal bool JiggleEnabled { get; private set; } = true;

    internal void SetJiggleEnabled(bool enabled)
    {
        JiggleEnabled = enabled;
        if (!enabled)
        {
            foreach (var player in SpotGame.Players)
            {
                StopPlayerJiggle(player);
            }
        }
    }

    internal bool CloudsEnabled { get; private set; } = true;

    internal void SetCloudsEnabled(bool enabled)
    {
        CloudsEnabled = enabled;

        if (enabled)
        {
            AddClouds();
        }
        else
        {
            _particleSurface?.Dispose();
            _particleSurface = null;
        }
    }

    /// <summary>
    /// Starts a new game with the supplied options, clearing the current scene first.
    /// </summary>
    internal void StartNewGame(NewGameOptions options)
    {
        _pendingComputerSelectTimer?.Dispose();
        _pendingComputerSelectTimer = null;
        _pendingComputerMoveTimer?.Dispose();
        _pendingComputerMoveTimer = null;

        _particleSurface = null;    // pre-null before ClearAll() disposes it, to avoid a double-dispose via AddClouds()
        Engine.Managers.DirectDrawings.ClearAll();
        Engine.Managers.Sprites.Clear();
        Scene.RemoveAllLayers();

        SetPlayerFrames(options.Players);

        var newGameResult = SpotGame.NewGame(options.BoardWidth, options.BoardHeight, options.Players.ToArray());

        Scene.AddLayer(newGameResult.Field);
        Scene.AddLayer(newGameResult.BackgroundField);

#if BROWSER
        if (_browserMusic != null) _browserMusic.Volume = 0.1f;
#else
        if (_music != null) _music.Volume = 0.1f;
#endif

        CreateTextBlockFields();
    }

    /// <summary>
    /// Starts a default 4-player game (1 human, 3 AI) on an 8×8 board.
    /// Useful as a quick-start shortcut; production UI should call
    /// <see cref="StartNewGame"/> with user-configured <see cref="NewGameOptions"/> instead.
    /// </summary>
    internal void StartDefaultGame()
    {
        var players = new List<Player>
        {
            new Player { Name = "Player 1", Type = PlayerType.Human,    ColorItem = new ColorItem("Blue",   SKColors.Blue,   SKColors.White) },
            new Player { Name = "Player 2", Type = PlayerType.Computer, ColorItem = new ColorItem("Red",    SKColors.Red,    SKColors.White) },
            new Player { Name = "Player 3", Type = PlayerType.Computer, ColorItem = new ColorItem("Green",  SKColors.Green,  SKColors.Black) },
            new Player { Name = "Player 4", Type = PlayerType.Computer, ColorItem = new ColorItem("Yellow", SKColors.Yellow, SKColors.Blue)  },
        };

        var options = new NewGameOptions
        {
            BoardWidth  = 8,
            BoardHeight = 8,
            Players     = players,
        };

        StartNewGame(options);
    }

    #endregion game settings

    #region private methods

    private void KeyboardEventPoller_KeyDown(KeyDownEventArgs args)
    {
        if (args.KeyAction != KeyAction.Pressed)
            return;

        // args.KeyConfig.Key holds the integer key code registered via StartMonitoringKey.
        if (int.TryParse(args.KeyConfig.Key, out int code) && code == (int)Key.S)
            SetScoreVisible(!_showScores);
    }

    private void MouseEventPoller_MouseEvent(Gondwana.Input.Mouse.MouseEventArgs args)
    {
        if (!_initialGameStarted && args.LeftButtonJustPressed)
        {
            RequestNewGameDialog?.Invoke();
            return;
        }

        if (!_handleHumanInput)
            return;

        if (Scene is null || Scene.SceneLayers.Count == 0)
            return;

        if (RenderSurface.Host.ViewManager.Views.Count == 0)
            return;

        var view = RenderSurface.Host.ViewManager.Views[0];
        var layer = Scene.SceneLayers[0];

        var screenPos = args.CurrentPosition;

        if (args.LeftButtonJustPressed)
        {
            var selectedCoord = view.ScreenPxToGrid(layer, screenPos);

            if (selectedCoord.X >= 0 && selectedCoord.X < layer.GridColumnCount &&
                selectedCoord.Y >= 0 && selectedCoord.Y < layer.GridRowCount)
            {
                var cell = SpotGame.SpotGameField.GetCell((int)selectedCoord.X, (int)selectedCoord.Y);

                if (SpotGame.AttemptSelectCell(cell, out var playerMovement) && playerMovement != null)
                    SpotGame.ExecuteMove(playerMovement.Value);
            }
        }
    }

    private void SetPlayerFrames(List<Player> players)
    {
        foreach (var player in players)
        {
            switch (player.ColorItem.Name)
            {
                case "Blue":
                    player.DefaultFrame = new Frame(_blueSpot, 0, 0);
                    player.ActiveFrame = new Frame(_blueSpotHappy, 0, 0);
                    break;
                case "Green":
                    player.DefaultFrame = new Frame(_greenSpot, 0, 0);
                    player.ActiveFrame = new Frame(_greenSpotHappy, 0, 0);
                    break;
                case "Violet":
                    player.DefaultFrame = new Frame(_pinkSpot, 0, 0);
                    player.ActiveFrame = new Frame(_pinkSpotHappy, 0, 0);
                    break;
                case "Red":
                    player.DefaultFrame = new Frame(_redSpot, 0, 0);
                    player.ActiveFrame = new Frame(_redSpotHappy, 0, 0);
                    break;
                case "Yellow":
                    player.DefaultFrame = new Frame(_yellowSpot, 0, 0);
                    player.ActiveFrame = new Frame(_yellowSpotHappy, 0, 0);
                    break;
                default:
                    break;
            }
        }
    }

    private void StartPlayerJiggle(Player player)
    {
        if (JiggleEnabled)
        {
            foreach (var cell in SpotGame.SpotGameField.GetAllCellsForPlayer(player))
            {
                cell.Sprite?.StartJiggle(loop: true);
            }
        }
    }

    private void StopPlayerJiggle(Player player)
    {
        foreach (var cell in SpotGame.SpotGameField.GetAllCellsForPlayer(player))
        {
            cell.Sprite?.StopJiggle();
        }
    }

    private void JiggleAllPlayers()
    {
        foreach (var player in SpotGame.Players)
        {
            StartPlayerJiggle(player);
        }
    }

    #endregion private methods

    #region particle emitters

    private ParticleEmitter GetSpots(float width, float height)
    {
        SKColor[] colors =
        {
            SKColors.Red,
            SKColors.Blue,
            SKColors.Yellow,
            SKColors.Green,
            SKColors.Violet
        };

        return new ParticleEmitter
        {
            Position = new PointF(width * 1.1f, height * 0.5f),
            JitterY = height * 0.5f,

            EmitRate = 0.65f,
            LifeRange = (1000f, 2000f),

            VelocityRangeX = (-100f, -50f),
            VelocityRangeY = (-1f, 1f),

            SizeRange = (40f, 80f),

            GravityY = 0f,
            BlendMode = SKBlendMode.SrcOver,

            OnSpawn = (ref Particle p) =>
            {
                var baseColor = colors[_rng.Next(colors.Length)];
                p.Color = baseColor.WithAlpha(255);
            }
        };
    }

    private void AddClouds()
    {
        if (SpotGame.Players.Length == 0)
            return;

        _particleSurface?.Dispose();
        _particleSurface = null;
        _particleSurface = new ParticleSurface(
            RenderSurface.Host,
            SpotGame.BackgroundGameField,
            new Rectangle(0, 0, 769, 769),
            "cloudSurface",
            4);

        _particleSurface.CullingMarginX = 1300f;
        _particleSurface.ZOrder = 50;
        _particleSurface.Emitters.Add(GetClouds(769, 769));
    }

    private ParticleEmitter GetClouds(float width, float height)
    {
        return new ParticleEmitter
        {
            Position = new PointF(width * 1.4f, height * 0.5f),
            JitterY = height * 0.5f,

            EmitRate = 0.075f,
            LifeRange = (2000f, 2000f),

            VelocityRangeX = (-50f, -25f),
            VelocityRangeY = (-1f, 1f),

            SizeRange = (200f, 500f),

            GravityY = 0f,
            BlendMode = SKBlendMode.SrcOver,

            ParticleSprite = _clouds.SkBitmap,

            OnSpawn = (ref Particle p) =>
            {
                p.AngularVel = 0;
                p.Rotation = 0;

                byte alpha = (byte)_rng.Next(100, 180);
                p.Tint = new SKColor(255, 255, 255, alpha);
            }
        };
    }

    #endregion particle emitters

    #region score display

    private void CreateTextBlockFields()
    {
        // upper left
        _player1Text = new TextBlock(RenderSurface.Host,
                                     RenderSurface.Host.ViewManager.Views[0],
                                     new Rectangle(10, 10, 200, 50));
        _player1Text.SetFont(_font, 24, 12)
                    .SetColors(SpotGame.Players[0].ColorItem.TextColor, SKColors.Transparent)
                    .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                    .SetText(SpotGame.Players[0].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[0]))
                    .SetMaxLines(1)
                    .UseShadow()
                    .SetShadow(3, 3, 200, 3.0f);
        _player1Text.ZOrder = 20;

        _player1Rectangle = new DirectRectangle(SpotGame.Players[0].ColorItem.Color.ToColor(),
                                                RenderSurface.Host,
                                                RenderSurface.Host.ViewManager.Views[0],
                                                _player1Text.ScreenBounds);
        _player1Rectangle.SetCornerRadius(30)
                         .SetFilled(true);

        // bottom right
        _player2Text = new TextBlock(RenderSurface.Host,
                                     RenderSurface.Host.ViewManager.Views[0],
                                     new Rectangle(RenderSurface.Host.Backbuffer.Width - 210, RenderSurface.Host.Backbuffer.Height - 60, 200, 50));
        _player2Text.SetFont(_font, 24, 12)
                    .SetColors(SpotGame.Players[1].ColorItem.TextColor, SKColors.Transparent)
                    .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                    .SetText(SpotGame.Players[1].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[1]))
                    .SetMaxLines(1)
                    .UseShadow()
                    .SetShadow(3, 3, 200, 3.0f);
        _player2Text.ZOrder = 20;

        _player2Rectangle = new DirectRectangle(SpotGame.Players[1].ColorItem.Color.ToColor(),
                                                RenderSurface.Host,
                                                RenderSurface.Host.ViewManager.Views[0],
                                                _player2Text.ScreenBounds);
        _player2Rectangle.SetCornerRadius(30)
                         .SetFilled(true);

        if (SpotGame.Players.Length >= 3)
        {
            // upper right
            _player3Text = new TextBlock(RenderSurface.Host,
                                         RenderSurface.Host.ViewManager.Views[0],
                                         new Rectangle(RenderSurface.Host.Backbuffer.Width - 210, 10, 200, 50));
            _player3Text.SetFont(_font, 24, 12)
                        .SetColors(SpotGame.Players[2].ColorItem.TextColor, SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                        .SetText(SpotGame.Players[2].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[2]))
                        .SetMaxLines(1)
                        .UseShadow()
                        .SetShadow(3, 3, 200, 3.0f);
            _player3Text.ZOrder = 20;

            _player3Rectangle = new DirectRectangle(SpotGame.Players[2].ColorItem.Color.ToColor(),
                                                    RenderSurface.Host,
                                                    RenderSurface.Host.ViewManager.Views[0],
                                                    _player3Text.ScreenBounds);
            _player3Rectangle.SetCornerRadius(30)
                             .SetFilled(true);
        }

        if (SpotGame.Players.Length >= 4)
        {
            // bottom left
            _player4Text = new TextBlock(RenderSurface.Host,
                                         RenderSurface.Host.ViewManager.Views[0],
                                         new Rectangle(10, RenderSurface.Host.Backbuffer.Height - 60, 200, 50));
            _player4Text.SetFont(_font, 24, 12)
                        .SetColors(SpotGame.Players[3].ColorItem.TextColor, SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                        .SetText(SpotGame.Players[3].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[3]))
                        .SetMaxLines(1)
                        .UseShadow()
                        .SetShadow(3, 3, 200, 3.0f);
            _player4Text.ZOrder = 20;

            _player4Rectangle = new DirectRectangle(SpotGame.Players[3].ColorItem.Color.ToColor(),
                                                    RenderSurface.Host,
                                                    RenderSurface.Host.ViewManager.Views[0],
                                                    _player4Text.ScreenBounds);
            _player4Rectangle.SetCornerRadius(30)
                             .SetFilled(true);
        }

        if (SpotGame.SpotGameField.GridColumnCount > 10 || SpotGame.SpotGameField.GridRowCount > 10)
        {
            SetScoreVisible(false);
        }
    }

    private void SetScoreVisible(bool visible)
    {
        _showScores = visible;

        if (_player1Text is not null)
        {
            _player1Text.Visible = visible;
            _player1Rectangle!.Visible = visible;
        }

        if (_player2Text is not null)
        {
            _player2Text.Visible = visible;
            _player2Rectangle!.Visible = visible;
        }

        if (_player3Text is not null)
        {
            _player3Text.Visible = visible;
            _player3Rectangle!.Visible = visible;
        }

        if (_player4Text is not null)
        {
            _player4Text.Visible = visible;
            _player4Rectangle!.Visible = visible;
        }

        if (visible)
            SetPlayerScores();
    }

    private void SetPlayerScores()
    {
        if (_player1Text is not null)
            _player1Text.SetText(SpotGame.Players[0].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[0]));

        if (_player2Text is not null)
            _player2Text.SetText(SpotGame.Players[1].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[1]));

        if (SpotGame.Players.Length >= 3)
            _player3Text?.SetText(SpotGame.Players[2].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[2]));

        if (SpotGame.Players.Length >= 4)
            _player4Text?.SetText(SpotGame.Players[3].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[3]));
    }

    private void CreateGameOverText(List<Player> winningPlayers)
    {
        bool multipleWinners;
        string message;

        Color primaryTextColor = winningPlayers[0].ColorItem.TextColor.ToColor();
        Color? secondaryTextColor = null;
        Color primaryFillColor = winningPlayers[0].ColorItem.Color.ToColor();
        Color? secondaryFillColor = null;

        if (winningPlayers.Count == 1)
        {
            multipleWinners = false;
            message = winningPlayers[0].Name + " wins!";
        }
        else
        {
            multipleWinners = true;

            var names = winningPlayers.Select(p => p.Name).ToList();
            var formatted = names.Count == 2
                ? string.Join(" and ", names)
                : string.Join(", ", names.Take(names.Count - 1)) + $", and {names.Last()}";

            message = $"{formatted} tie!";

            secondaryTextColor = winningPlayers[1].ColorItem.TextColor.ToColor();
            secondaryFillColor = winningPlayers[1].ColorItem.Color.ToColor();
        }

        _gameMessageText = new TextBlock(RenderSurface.Host,
                                         RenderSurface.Host.ViewManager.Views[0],
                                         new Rectangle(RenderSurface.Host.Backbuffer.Width / 2 - 180, RenderSurface.Host.Backbuffer.Height / 2 - 40, 360, 80));
        _gameMessageText.SetFont(_font, 48, 16)
                        .SetColors(primaryTextColor.ToSKColor(), SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                        .SetText(message)
                        .UseShadow()
                        .SetShadow(5, 5, 200, 3.0f)
                        .EnableWrapping();

        _gameMessageText.ZOrder = 20;

        _gameMessageRectangle = new DirectRectangle(primaryFillColor,
                                                    RenderSurface.Host,
                                                    RenderSurface.Host.ViewManager.Views[0],
                                                    _gameMessageText.ScreenBounds);
        _gameMessageRectangle.SetCornerRadius(40)
                             .SetFilled(true)
                             .SetColor(primaryFillColor)
                             .SetBorderColor(primaryTextColor)
                             .SetStrokeWidth(2f)
                             .SetStrokeAlign(DirectRectangle.StrokeAlign.Outside);

        if (multipleWinners)
        {
            _gameMessageText.PulseColor(primaryTextColor, secondaryTextColor!.Value, 1.75f);
            _gameMessageRectangle.PulseFill(primaryFillColor, secondaryFillColor!.Value, 1.25f);
            _gameMessageRectangle.PulseBorder(primaryTextColor, secondaryTextColor.Value, 0.75f);
        }
    }

    #endregion score display

    #region SpotGame event handlers

    private void HookSpotGameEvents()
    {
        if (SpotGame is null)
            return;

        SpotGame.GameStarted += OnGameStarted;
        SpotGame.PlayerTurnStarted += OnPlayerTurnStarted;
        SpotGame.PlayerTurnEnded += OnPlayerTurnEnded;
        SpotGame.SpotSelected += OnSpotSelected;
        SpotGame.SpotDeselected += OnSpotDeselected;
        SpotGame.InvalidSelectionAttempted += OnInvalidSelectionAttempted;
        SpotGame.InvalidMoveAttempted += OnInvalidMoveAttempted;
        SpotGame.PlayerMoveStarted += OnPlayerMoveStarted;
        SpotGame.PlayerMoveStopped += OnPlayerMoveStopped;
        SpotGame.CellsCaptured += OnCellsCaptured;
        SpotGame.NoValidMovesAvailable += OnNoValidMovesAvailable;
        SpotGame.GameOver += OnGameOver;
    }

    private void UnhookSpotGameEvents()
    {
        if (SpotGame is null)
            return;

        SpotGame.GameStarted -= OnGameStarted;
        SpotGame.PlayerTurnStarted -= OnPlayerTurnStarted;
        SpotGame.PlayerTurnEnded -= OnPlayerTurnEnded;
        SpotGame.SpotSelected -= OnSpotSelected;
        SpotGame.SpotDeselected -= OnSpotDeselected;
        SpotGame.InvalidSelectionAttempted -= OnInvalidSelectionAttempted;
        SpotGame.InvalidMoveAttempted -= OnInvalidMoveAttempted;
        SpotGame.PlayerMoveStarted -= OnPlayerMoveStarted;
        SpotGame.PlayerMoveStopped -= OnPlayerMoveStopped;
        SpotGame.CellsCaptured -= OnCellsCaptured;
        SpotGame.NoValidMovesAvailable -= OnNoValidMovesAvailable;
        SpotGame.GameOver -= OnGameOver;
    }

    private void OnGameStarted(SpotGame game)
    {
        Engine.Logger.LogDebug("Game started with players: {0}", string.Join(", ", game.Players.Select(p => p.Name)));

        _initialGameStarted = true;

        if (MusicEnabled)
        {
#if BROWSER
            _browserMusic?.Play(fromStart: false);
#else
            if (_music != null && !_music.IsPlaying)
                _music.Play();
#endif
        }

        if (CloudsEnabled)
            AddClouds();
    }

    private void OnPlayerTurnStarted(Player player)
    {
        Engine.Logger.LogDebug("Player {0}'s turn started", player.Name);
        StartPlayerJiggle(player);

        if (player.Type == PlayerType.Human)
        {
            _handleHumanInput = true;
        }
        else
        {
            _handleHumanInput = false;

            // start a short timer before the computer moves
            _pendingComputerSelectTimer = Gondwana.Timers.Timer.Add(TimerType.PostCycle, TimerCycles.Once, 0.6);
            _pendingComputerSelectTimer.Tick += () =>
            {
_pendingComputerSelectTimer?.Dispose();
_pendingComputerSelectTimer = null;

                var moves = SpotGame.SpotGameField.GetBestMovesForPlayer(player);
                if (moves.Count == 0)
                    return;

                var bestMove = moves[_rng.Next(moves.Count)];

                SpotGame.AttemptSelectCell(bestMove.FromCell, out _);

                // small delay before executing move to allow for selection animation
                _pendingComputerMoveTimer = Gondwana.Timers.Timer.Add(TimerType.PostCycle, TimerCycles.Once, 0.6);
                _pendingComputerMoveTimer.Tick += () =>
                {
_pendingComputerMoveTimer?.Dispose();
_pendingComputerMoveTimer = null;
                    SpotGame.ExecuteMove(bestMove);
                };
            };
        }
    }

    private void OnPlayerTurnEnded(Player player)
    {
        Engine.Logger.LogDebug("Player {0}'s turn ended", player.Name);
        StopPlayerJiggle(player);
    }

    private void OnSpotSelected(SpotGameField.Cell cell)
    {
        Engine.Logger.LogDebug("Cell at ({0}, {1}) selected by player {2}", cell.X, cell.Y, cell.OccupiedBy!.Name);

        if (SoundEffectsEnabled && SpotGame.CurrentPlayer.Type == PlayerType.Human)
            _spotSelected?.Play();

        var sprite = cell.Sprite!;
        sprite.StopJiggle();
        sprite.CurrentFrame = cell.OccupiedBy.ActiveFrame;
        sprite.PulseBy(1.1f, 0.4f, 0.4f, true);
    }

    private void OnSpotDeselected(SpotGameField.Cell cell)
    {
        Engine.Logger.LogDebug("Cell at ({0}, {1}) deselected", cell.X, cell.Y);

        if (SoundEffectsEnabled)
            _spotDeselected?.Play();

        var sprite = cell.Sprite!;
        sprite.StartJiggle(loop: true);
        sprite.CurrentFrame = cell.OccupiedBy!.DefaultFrame;
        sprite.StopPulse(true, 0.2f);
    }

    private void OnInvalidSelectionAttempted(SpotGameField.Cell cell)
    {
        Engine.Logger.LogDebug("Invalid selection attempted at cell ({0}, {1})", cell.X, cell.Y);

        if (SoundEffectsEnabled)
        {
#if BROWSER
            _browserBump?.Play();
#else
            _bump?.Play();
#endif
        }
    }

    private void OnInvalidMoveAttempted(SpotGameField.Cell cell)
    {
        Engine.Logger.LogDebug("Invalid move attempted to cell ({0}, {1})", cell.X, cell.Y);

        if (SoundEffectsEnabled)
            _knock?.Play();
    }

    private void OnPlayerMoveStarted(PlayerMovement movement)
    {
        if (movement.MovementType == MovementType.Jump && SoundEffectsEnabled)
        {
#if BROWSER
            _browserVelcro?.Play();
#else
            _velcro?.Play();
#endif
        }
    }

    private void OnPlayerMoveStopped(PlayerMovement movement)
    {
        Engine.Logger.LogDebug("Player {0} performed a {1} move from ({2}, {3}) to ({4}, {5})",
            movement.Player.Name,
            movement.MovementType,
            movement.FromX, movement.FromY,
            movement.DestX, movement.DestY);

        if (SoundEffectsEnabled)
        {
#if BROWSER
            _browserDrop?.Play();
#else
            _drop?.Play();
#endif
        }

        if (_showScores)
            SetPlayerScores();

        SpotGame.NextPlayer();
    }

    private void OnCellsCaptured(List<SpotGameField.Cell> cellsCaptured)
    {
        Engine.Logger.LogDebug("{0} cells captured", cellsCaptured.Count);

        foreach (var cell in cellsCaptured)
        {
            var oldSprite = cell.Sprite;
            if (oldSprite == null)
                continue;

            Action? handler = null;
            handler = () =>
            {
                oldSprite.ResizeComplete -= handler;
                oldSprite.CurrentFrame = cell.OccupiedBy!.DefaultFrame;
                oldSprite.ResizeTo(new(56, 56), 0.2f);
            };

            oldSprite.ResizeComplete += handler;
            oldSprite.ResizeTo(new(1, 1), 0.2f);
        }
    }

    private void OnNoValidMovesAvailable(Player player)
    {
        Engine.Logger.LogDebug("No valid moves available for player {0}", player.Name);

        SpotGame.NextPlayer();
    }

    private void OnGameOver()
    {
        Engine.Logger.LogDebug("Game over");

        _handleHumanInput = false;

        SetScoreVisible(true);
        SetPlayerScores();
        StopPlayerJiggle(SpotGame.CurrentPlayer);
        JiggleAllPlayers();

        var allScores = SpotGame.GetAllPlayerScores();
        var maxScore = allScores.Values.Max();
        var winnersWithScores = allScores
            .Where(kvp => kvp.Value == maxScore)
            .Select(kvp => kvp.Key)
            .ToList();

        CreateGameOverText(winnersWithScores);

        if (MusicEnabled)
        {
#if BROWSER
            if (_browserMusic != null) _browserMusic.Volume = 0.05f;

            var isHumanWinner = winnersWithScores.Any(winner => winner.Type == PlayerType.Human);
            if (isHumanWinner)
                _browserGameWin?.Play();
            else
                _browserGameLose?.Play();
#else
            if (_music != null)
            {
                _music.Volume = 0.05f;

                var isHumanWinner = winnersWithScores.Any(winner => winner.Type == PlayerType.Human);
                if (isHumanWinner)
                    _gameWin?.Play();
                else
                    _gameLose?.Play();
            }
#endif
        }

#if !BROWSER
        Engine.Instance.State.SaveToFile("savegame.json", false, true);
#endif
    }

    #endregion SpotGame event handlers
}
