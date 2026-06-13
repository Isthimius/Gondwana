using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Gondwana.Audio;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Direct.Particles;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Hosting;
using Gondwana.Input.Keyboard;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using Gondwana.Timers;
using Gondwana.WinForms.Input.Keyboard;
using Gondwana.Demos.Spot.Game;
using Gondwana.Rendering;

namespace Gondwana.Demos.Spot;

/// <summary>
/// Contains all Spot game host behavior, independent of the rendering backend.
/// Both <see cref="SpotGameHost"/> (bitmap) and <see cref="SpotGpuGameHost"/> (GPU)
/// delegate to an instance of this class and expose themselves as <see cref="ISpotHostContext"/>.
/// This is only needed to allow for both Bitmap and GPU rendering from the same project.
/// </summary>
internal sealed class SpotHostCore
{
    private readonly ISpotHostContext _ctx;

    private Engine Engine => _ctx.Engine;
    private Scene Scene => _ctx.Scene;
    private RenderSurfaceHostBase SurfaceHost => _ctx.SurfaceHost;
    private int SurfaceWidth => _ctx.SurfaceWidth;
    private int SurfaceHeight => _ctx.SurfaceHeight;

    private bool _initialGameStarted = false;
    private bool _handleHumanInput = false;
    private bool _showScores = true;
    private NewGameOptions? _lastNewGameOptions;

    private ParticleSurface? _particleSurface;

    private Gondwana.Timers.Timer? _pendingComputerSelectTimer;
    private Gondwana.Timers.Timer? _pendingComputerMoveTimer;

    private TextBlock? _player1Text;
    private DirectRectangle? _player1Rectangle;
    private TextBlock? _player2Text;
    private DirectRectangle? _player2Rectangle;
    private TextBlock? _player3Text;
    private DirectRectangle? _player3Rectangle;
    private TextBlock? _player4Text;
    private DirectRectangle? _player4Rectangle;
    private TextBlock? _gameMessageText;
    private DirectRectangle? _gameMessageRectangle;

    private AudioResource _music = null!;

    private AudioResource? _spotSelected;
    private AudioResource? _spotDeselected;
    private AudioResource _velcro = null!;
    private AudioResource _drop = null!;
    private AudioResource _gameWin = null!;
    private AudioResource _gameLose = null!;
    private AudioResource _bump = null!;
    private AudioResource? _knock;

    private Tilesheet _spotSheetDefault = null!;
    private Tilesheet _spotSheetSelected = null!;

    private Tilesheet _clouds = null!;

    private SKTypeface _font = null!;

    internal SpotGame SpotGame { get; private set; } = null!;

    private static readonly Random _rng = new();
    private bool _startupPresentationShown = false;

    public NewGameOptions? LastNewGameOptions { get => _lastNewGameOptions; }
    public bool MusicEnabled { get; private set; } = true;
    public bool SoundEffectsEnabled { get; private set; } = true;
    public bool JiggleEnabled { get; private set; } = true;
    public bool CloudsEnabled { get; private set; } = true;

    internal SpotHostCore(ISpotHostContext context)
    {
        _ctx = context;
    }

    #region WinFormsGameHost lifecycle hooks

    internal SplashScreen? CreateSplash(Gondwana.Rendering.RenderSurfaceHostBase host)
    {
        var imagePath = Path.Combine(AppContext.BaseDirectory, "assets", "gondwana-logo-text.png");
        var splash = SplashScreen.TryCreate(host, imagePath);
        if (splash != null)
            splash.HoldSec = 3f;
        return splash;
    }

    internal void LoadAssets()
    {
        // load standalone audio files
        _music = Engine.Managers.AudioResources.LoadFromFile("music", "assets\\sounovamusic-puzzle-amp-casual-game-music-460543.mp3");
        _music.IsLooping = true;

        _spotSelected = Engine.Managers.AudioResources.LoadFromFile("spotSelected", "assets\\universfield-bubble-pop-293342.mp3");
        _spotSelected.Volume = 0.4f;

        _spotDeselected = Engine.Managers.AudioResources.LoadFromFile("spotDeselected", "assets\\universfield-bubble-pop-293342.mp3");
        _spotDeselected.Volume = 0.15f;

        _velcro = Engine.Managers.AudioResources.LoadFromFile("velcro", "assets\\freesound_community-velcro_fast-91558.mp3");
        _drop = Engine.Managers.AudioResources.LoadFromFile("drop", "assets\\freesound_community-water-drip-45622.mp3");
        _gameWin = Engine.Managers.AudioResources.LoadFromFile("gameWin", "assets\\peekaboolabcreative-11l-victory_sound_with_t-1749487402950-357606.mp3");
        _gameLose = Engine.Managers.AudioResources.LoadFromFile("gameLose", "assets\\freesound_community-080047_lose_funny_retro_video-game-80925.mp3");
        _bump = Engine.Managers.AudioResources.LoadFromFile("bump", "assets\\freesound_community-bump-7-92964.mp3");
        _knock = Engine.Managers.AudioResources.LoadFromFile("knock", "assets\\rohhsadotcom-knock-on-wood-02-421991.mp3");

        // load standalone video files

        // load standalone font files
        _font = Engine.Managers.Fonts.LoadFromFile("main", "assets\\ArchitectsDaughter-Regular.ttf");

        // load standalone cursor files
    }

    internal void LoadTilesheets()
    {
        // splash logo
        var splash = TilesheetRegistry.Instance.LoadFromImageFile("splash", "assets\\spot.png");
        splash.ApplyMask(Color.Black.ToSKColor());

        _spotSheetDefault = TilesheetRegistry.Instance.LoadFromImageFile("spots", "assets\\spot_defaults.png");
        _spotSheetDefault.DefaultRegion.TileSize = new Size(93, 96);

        _spotSheetSelected = TilesheetRegistry.Instance.LoadFromImageFile("selected", "assets\\spot_selected.png");
        _spotSheetSelected.DefaultRegion.TileSize = new Size(64, 64);

        _clouds = TilesheetRegistry.Instance.LoadFromImageFile("clouds", "assets\\clouds.png");
    }

    internal Scene CreateInitialScene()
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

    /// <summary>
    /// Called from the adapter's <c>CreateSceneGraph</c> override, after <c>base.CreateSceneGraph()</c>.
    /// </summary>
    internal void CreateSceneGraph()
    {
        SurfaceHost.Backbuffer.ClearColor = Color.CornflowerBlue.ToSKColor();

        SpotGame = new SpotGame();
        HookSpotGameEvents();
    }

    internal void OnMouseAdapterInitialized()
    {
        if (Engine.Input.MouseEventPoller is null)
            return;

        Engine.Input.MouseEventPoller.MouseEvent += MouseEventPoller_MouseEvent;
        Engine.Input.MouseEventPoller.StartMonitoringMouse();
    }

    internal void OnKeyboardAdapterInitialized()
    {
        if (Engine.Input.KeyboardEventPoller is null)
            return;

        Engine.Input.KeyboardEventPoller.KeyDown += KeyboardEventPoller_KeyDown;
        Engine.Input.KeyboardEventPoller.StartMonitoringKey((int)Keys.Oemtilde);
    }

    internal void UnhookEvents()
    {
        if (Engine.Input.MouseEventPoller is not null)
            Engine.Input.MouseEventPoller.MouseEvent -= MouseEventPoller_MouseEvent;

        if (Engine.Input.KeyboardEventPoller is not null)
            Engine.Input.KeyboardEventPoller.KeyDown -= KeyboardEventPoller_KeyDown;

        UnhookSpotGameEvents();
    }

    #endregion WinFormsGameHost lifecycle hooks

    #region public game interface

    internal void BeginPostSplashStartup()
    {
        if (_startupPresentationShown)
            return;

        _startupPresentationShown = true;

        Tilesheet tilesheet;

        if (TilesheetRegistry.Instance.TryGet("splash", out tilesheet))
        {
            var directImage = new DirectImage(
                tilesheet.SkBitmap,
                SurfaceHost,
                Scene[0],
                new Rectangle(0, 0, 769, 769));

            directImage.ZOrder = 100;
            directImage.SetScaleMode(DirectImage.ScaleMode.Fit);
        }

        var particleSurface = new ParticleSurface(
            SurfaceHost,
            Scene[0],
            new Rectangle(0, 0, 769, 769));

        particleSurface.CullingMarginX = 1300f;
        particleSurface.ZOrder = 50;
        particleSurface.Emitters.Add(GetSpots(769, 769));

        if (MusicEnabled)
        {
            _music.Volume = 0.2f;
            if (!_music.IsPlaying)
                _music.Play();
        }
    }

public void OpenNewGameDialog(NewGameOptions? newGameOptions = null)
{
    if (Engine.UiDispatcher is not null && !Engine.UiDispatcher.IsOnUIThread)
    {
        Engine.UiDispatcher.Post(() => OpenNewGameDialog(newGameOptions));
        return;
    }

    using var dialog = new NewGameDialog(newGameOptions);
    if (dialog.ShowDialog() == DialogResult.OK)
    {
        _lastNewGameOptions = dialog.Options;
        var options = dialog.Options;
        Engine.EngineDispatcher.Post(() => StartNewGame(options));
    }
    else
    {
        _lastNewGameOptions = dialog.Options;
    }
}

    #endregion public game interface

    #region game settings

    internal void SetMusicEnabled(bool enabled)
    {
        MusicEnabled = enabled;

        if (enabled)
        {
            if (!_music.IsPlaying)
                _music.Play();
        }
        else
        {
            _music.Stop();
        }
    }

    internal void SetSoundEffectsEnabled(bool enabled)
    {
        SoundEffectsEnabled = enabled;
    }

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

    internal void SetCloudsEnabled(bool enabled)
    {
        CloudsEnabled = enabled;

        if (enabled)
        {
            DisposeParticleSurface();
            AddClouds();
        }
        else
        {
            DisposeParticleSurface();
        }
    }

    internal void StartNewGame(NewGameOptions options)
    {
        _pendingComputerSelectTimer?.Dispose();
        _pendingComputerSelectTimer = null;
        _pendingComputerMoveTimer?.Dispose();
        _pendingComputerMoveTimer = null;

        _particleSurface = null;    // pre-null before ClearAll() disposes it, to avoid a double-dispose via DisposeParticleSurface()
        Engine.Managers.DirectDrawings.ClearAll();
        Engine.Managers.Sprites.Clear();
        Scene.RemoveAllLayers();

        SetPlayerFrames(options.Players);

        var newGameResult = SpotGame.NewGame(options.BoardWidth, options.BoardHeight, options.Players.ToArray());

        Scene.AddLayer(newGameResult.Field);
        Scene.AddLayer(newGameResult.BackgroundField);
        _music.Volume = 0.1f;

        CreateTextBlockFields();
    }

    #endregion game settings

    #region private methods

    private void KeyboardEventPoller_KeyDown(KeyDownEventArgs args)
    {
        if (args.KeyAction != KeyAction.Pressed)
            return;

        var key = WinFormsKeyboardAdapter.GetKeyFromString(args.KeyConfig.Key);
        switch (key)
        {
            case Keys.Oemtilde:
                SetScoreVisible(!_showScores);
                break;
            default:
                break;
        }
    }

    private void MouseEventPoller_MouseEvent(Gondwana.Input.Mouse.MouseEventArgs args)
    {
        if (!_initialGameStarted && args.LeftButtonJustPressed)
        {
            OpenNewGameDialog(_lastNewGameOptions);
            return;
        }

        if (!_handleHumanInput)
            return;

        if (Scene is null || Scene.SceneLayers.Count == 0)
            return;

        if (SurfaceHost.ViewManager.Views.Count == 0)
            return;

        var view = SurfaceHost.ViewManager.Views[0];
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
                    player.DefaultFrame = new Frame(_spotSheetDefault, 0, 0);
                    player.ActiveFrame = new Frame(_spotSheetSelected, 0, 0);
                    break;
                case "Green":
                    player.DefaultFrame = new Frame(_spotSheetDefault, 0, 1);
                    player.ActiveFrame = new Frame(_spotSheetSelected, 1, 0);
                    break;
                case "Violet":
                    player.DefaultFrame = new Frame(_spotSheetDefault, 0, 2);
                    player.ActiveFrame = new Frame(_spotSheetSelected, 2, 0);
                    break;
                case "Red":
                    player.DefaultFrame = new Frame(_spotSheetDefault, 0, 3);
                    player.ActiveFrame = new Frame(_spotSheetSelected, 3, 0);
                    break;
                case "Yellow":
                    player.DefaultFrame = new Frame(_spotSheetDefault, 0, 4);
                    player.ActiveFrame = new Frame(_spotSheetSelected, 4, 0);
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

    private void DisposeParticleSurface()
    {
        _particleSurface?.Dispose();
        _particleSurface = null;
    }

    private void AddClouds()
    {
        if (SpotGame.Players.Length == 0)
            return;

        DisposeParticleSurface();
        _particleSurface = new ParticleSurface(
            SurfaceHost,
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

                byte alpha = (byte)Random.Shared.Next(100, 180);
                p.Tint = new SKColor(255, 255, 255, alpha);
            }
        };
    }

    #endregion particle emitters

    #region score display

    private void CreateTextBlockFields()
    {
        // upper left
        _player1Text = new TextBlock(SurfaceHost,
                                     SurfaceHost.ViewManager.Views[0],
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
                                                SurfaceHost,
                                                SurfaceHost.ViewManager.Views[0],
                                                _player1Text.ScreenBounds);
        _player1Rectangle.SetCornerRadius(30)
                         .SetFilled(true);

        // bottom right
        _player2Text = new TextBlock(SurfaceHost,
                                     SurfaceHost.ViewManager.Views[0],
                                     new Rectangle(SurfaceWidth - 210, SurfaceHeight - 60, 200, 50));
        _player2Text.SetFont(_font, 24, 12)
                    .SetColors(SpotGame.Players[1].ColorItem.TextColor, SKColors.Transparent)
                    .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                    .SetText(SpotGame.Players[1].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[1]))
                    .SetMaxLines(1)
                    .UseShadow()
                    .SetShadow(3, 3, 200, 3.0f);
        _player2Text.ZOrder = 20;

        _player2Rectangle = new DirectRectangle(SpotGame.Players[1].ColorItem.Color.ToColor(),
                                                SurfaceHost,
                                                SurfaceHost.ViewManager.Views[0],
                                                _player2Text.ScreenBounds);
        _player2Rectangle.SetCornerRadius(30)
                         .SetFilled(true);

        if (SpotGame.Players.Length >= 3)
        {
            // upper right
            _player3Text = new TextBlock(SurfaceHost,
                                         SurfaceHost.ViewManager.Views[0],
                                         new Rectangle(SurfaceWidth - 210, 10, 200, 50));
            _player3Text.SetFont(_font, 24, 12)
                        .SetColors(SpotGame.Players[2].ColorItem.TextColor, SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                        .SetText(SpotGame.Players[2].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[2]))
                        .SetMaxLines(1)
                        .UseShadow()
                        .SetShadow(3, 3, 200, 3.0f);
            _player3Text.ZOrder = 20;

            _player3Rectangle = new DirectRectangle(SpotGame.Players[2].ColorItem.Color.ToColor(),
                                                    SurfaceHost,
                                                    SurfaceHost.ViewManager.Views[0],
                                                    _player3Text.ScreenBounds);
            _player3Rectangle.SetCornerRadius(30)
                             .SetFilled(true);
        }

        if (SpotGame.Players.Length >= 4)
        {
            // bottom left
            _player4Text = new TextBlock(SurfaceHost,
                                         SurfaceHost.ViewManager.Views[0],
                                         new Rectangle(10, SurfaceHeight - 60, 200, 50));
            _player4Text.SetFont(_font, 24, 12)
                        .SetColors(SpotGame.Players[3].ColorItem.TextColor, SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                        .SetText(SpotGame.Players[3].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[3]))
                        .SetMaxLines(1)
                        .UseShadow()
                        .SetShadow(3, 3, 200, 3.0f);

            _player4Text.ZOrder = 20;

            _player4Rectangle = new DirectRectangle(SpotGame.Players[3].ColorItem.Color.ToColor(),
                                                    SurfaceHost,
                                                    SurfaceHost.ViewManager.Views[0],
                                                    _player4Text.ScreenBounds);
            _player4Rectangle.SetCornerRadius(30)
                             .SetFilled(true);
        }

        if (SpotGame.SpotGameField.GridRowCount > 10)
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

        _gameMessageText = new TextBlock(SurfaceHost,
                                         SurfaceHost.ViewManager.Views[0],
                                         new Rectangle(SurfaceWidth / 2 - 180, SurfaceHeight / 2 - 40, 360, 80));
        _gameMessageText.SetFont(_font, 48, 16)
                        .SetColors(primaryTextColor.ToSKColor(), SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                        .SetText(message)
                        .UseShadow()
                        .SetShadow(5, 5, 200, 3.0f)
                        .EnableWrapping();

        _gameMessageText.ZOrder = 20;

        _gameMessageRectangle = new DirectRectangle(primaryFillColor,
                                                    SurfaceHost,
                                                    SurfaceHost.ViewManager.Views[0],
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

        if (MusicEnabled && !_music.IsPlaying)
            _music.Play();

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

            // start a short timer before computer moves
            _pendingComputerSelectTimer = Gondwana.Timers.Timer.Add(TimerType.PostCycle, TimerCycles.Once, 0.6);
            _pendingComputerSelectTimer.Tick += () =>
            {
                _pendingComputerSelectTimer = null;

                var moves = SpotGame.SpotGameField.GetBestMovesForPlayer(player);
                if (moves.Count == 0)
                    return;

                var bestMove = moves[Random.Shared.Next(moves.Count)];

                SpotGame.AttemptSelectCell(bestMove.FromCell, out _);

                // small delay before executing move to allow for selection animation
                _pendingComputerMoveTimer = Gondwana.Timers.Timer.Add(TimerType.PostCycle, TimerCycles.Once, 0.6);
                _pendingComputerMoveTimer.Tick += () =>
                {
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
            _bump?.Play();
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
            _velcro?.Play();
    }

    private void OnPlayerMoveStopped(PlayerMovement movement)
    {
        Engine.Logger.LogDebug("Player {0} performed a {1} move from ({2}, {3}) to ({4}, {5})",
            movement.Player.Name,
            movement.MovementType,
            movement.FromX, movement.FromY,
            movement.DestX, movement.DestY);

        if (SoundEffectsEnabled)
            _drop?.Play();

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
            _music.Volume = 0.05f;

            var isHumanWinner = winnersWithScores.Any(winner => winner.Type == PlayerType.Human);
            if (isHumanWinner)
                _gameWin?.Play();
            else
                _gameLose?.Play();
        }

        Engine.Instance.State.SaveToFile("savegame.json", false, true);
    }

    #endregion SpotGame event handlers
}
