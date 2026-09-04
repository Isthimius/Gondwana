using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using SkiaSharp;
using Gondwana;
using Gondwana.Audio.Browser;
using Gondwana.Blazor.Hosting;
using Gondwana.Blazor.Rendering;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Direct.Particles;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using Gondwana.Timers;
using Gondwana.Demos.SpotBlazor.Game;

namespace Gondwana.Demos.SpotBlazor;

/// <summary>
/// Game host for Spot.Blazor. Extends <see cref="BlazorGpuGameHost"/> and uses
/// <see cref="BlazorGpuRenderSurfaceComponent"/> (GpuBackbuffer) to render the game.
/// Assets are loaded from streams obtained via <see cref="HttpClient"/> before initialization.
/// </summary>
internal sealed class SpotBlazorGameHost : BlazorGpuGameHost
{
    private readonly Dictionary<string, byte[]> _assetData;

    private bool _handleHumanInput = false;
    private bool _showScores = true;

    private ParticleSurface? _particleSurface;

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

    private Tilesheet _spotSheetDefault = null!;
    private Tilesheet _spotSheetSelected = null!;
    private Tilesheet _clouds = null!;
    private SKTypeface _font = null!;

    internal SpotGame SpotGame { get; private set; } = null!;

    private static Random _rng => Random.Shared;

    internal bool MusicEnabled { get; private set; } = true;
    internal bool SoundEffectsEnabled { get; private set; } = true;
    internal bool JiggleEnabled { get; private set; } = false;
    internal bool CloudsEnabled { get; private set; } = false;

    /// <summary>
    /// Initializes a new instance of <see cref="SpotBlazorGameHost"/>.
    /// </summary>
    /// <param name="renderSurface">The Blazor WebGL render surface component.</param>
    /// <param name="jsRuntime">The JavaScript runtime for interop.</param>
    /// <param name="assetData">Pre-loaded asset byte arrays keyed by filename (e.g. "spot_defaults.png").</param>
    internal SpotBlazorGameHost(BlazorGpuRenderSurfaceComponent renderSurface, IJSRuntime jsRuntime, Dictionary<string, byte[]> assetData)
        : base(renderSurface, jsRuntime)
    {
        _assetData = assetData;
    }

    #region BlazorGameHost overrides

    protected override void LoadAssets()
    {
        // Font
        if (_assetData.TryGetValue("ArchitectsDaughter-Regular.ttf", out var fontData))
        {
            using var fontStream = new MemoryStream(fontData);
            _font = SKTypeface.FromStream(fontStream) ?? SKTypeface.Default;
        }
        else
        {
            _font = SKTypeface.Default;
        }

        // Audio - use BrowserAudioManager for browser/WASM targets
        var audioManager = Engine.Instance.GetBrowserAudioManager();

        // Music
        audioManager.Load("music", "assets/sounovamusic-puzzle-amp-casual-game-music-460543.mp3", loop: true, volume: 1.0f);

        // Sound effects
        audioManager.Load("spotSelected", "assets/universfield-bubble-pop-293342.mp3", loop: false, volume: 0.4f);
        audioManager.Load("spotDeselected", "assets/universfield-bubble-pop-293342.mp3", loop: false, volume: 0.15f);
        audioManager.Load("velcro", "assets/freesound_community-velcro_fast-91558.mp3", loop: false, volume: 1.0f);
        audioManager.Load("drop", "assets/freesound_community-water-drip-45622.mp3", loop: false, volume: 1.0f);
        audioManager.Load("gameWin", "assets/peekaboolabcreative-11l-victory_sound_with_t-1749487402950-357606.mp3", loop: false, volume: 1.0f);
        audioManager.Load("gameLose", "assets/freesound_community-080047_lose_funny_retro_video-game-80925.mp3", loop: false, volume: 1.0f);
        audioManager.Load("bump", "assets/freesound_community-bump-7-92964.mp3", loop: false, volume: 1.0f);
        audioManager.Load("knock", "assets/rohhsadotcom-knock-on-wood-02-421991.mp3", loop: false, volume: 1.0f);

        // Start background music if enabled
        if (MusicEnabled)
        {
            audioManager.Get("music").Play(fromStart: true);
        }
    }

    protected override void LoadTilesheets()
    {
        if (_assetData.TryGetValue("spot.png", out var splashData))
        {
            using var stream = new MemoryStream(splashData);
            var splash = TilesheetRegistry.Instance.LoadFromStream("splash", stream);
            splash.ApplyMask(Color.Black.ToSKColor());
        }

        if (_assetData.TryGetValue("spot_defaults.png", out var defaultsData))
        {
            using var stream = new MemoryStream(defaultsData);
            _spotSheetDefault = TilesheetRegistry.Instance.LoadFromStream("spots", stream);
            _spotSheetDefault.DefaultRegion.TileSize = new Size(93, 96);
        }

        if (_assetData.TryGetValue("spot_selected.png", out var selectedData))
        {
            using var stream = new MemoryStream(selectedData);
            _spotSheetSelected = TilesheetRegistry.Instance.LoadFromStream("selected", stream);
            _spotSheetSelected.DefaultRegion.TileSize = new Size(64, 64);
        }

        if (_assetData.TryGetValue("clouds.png", out var cloudsData))
        {
            using var stream = new MemoryStream(cloudsData);
            _clouds = TilesheetRegistry.Instance.LoadFromStream("clouds", stream);
        }
    }

    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();

        scene.AddLayer(
            columnCount: 1,
            rowCount: 1,
            width: 768,
            height: 768,
            zOrder: 10,
            parallax: 1f,
            coordinateSystem: CoordinateSystemTypes.Orthogonal);

        return scene;
    }

    protected override void OnSceneGraphCreated()
    {
        RenderSurface.Host.Backbuffer.ClearColor = Color.CornflowerBlue.ToSKColor();

        SpotGame = new SpotGame();
        HookSpotGameEvents();
    }

    protected override void CreateDirectDrawings()
    {
        if (TilesheetRegistry.Instance.TryGet("splash", out var tilesheet))
        {
            var directImage = new DirectImage(
                tilesheet.SkBitmap,
                RenderSurface.Host,
                Scene![0],
                new Rectangle(0, 0, 769, 769));

            directImage.ZOrder = 100;
            directImage.SetScaleMode(DirectImage.ScaleMode.Fit);
        }

        var particleSurface = new ParticleSurface(
            RenderSurface.Host,
            Scene![0],
            new Rectangle(0, 0, 769, 769));

        particleSurface.CullingMarginX = 1300f;
        particleSurface.ZOrder = 50;
        particleSurface.Emitters.Add(GetSpots(769, 769));
    }

    protected override void OnMouseAdapterInitialized()
    {
        if (Engine.Input.MouseEventPoller is null)
            return;

        Engine.Input.MouseEventPoller.MouseEvent += MouseEventPoller_MouseEvent;
        Engine.Input.MouseEventPoller.StartMonitoringMouse();
    }

    protected override void UnhookEvents()
    {
        if (Engine.Input.MouseEventPoller is not null)
            Engine.Input.MouseEventPoller.MouseEvent -= MouseEventPoller_MouseEvent;

        UnhookSpotGameEvents();
    }

    #endregion BlazorGameHost overrides

    #region game settings

    internal void SetMusicEnabled(bool enabled)
    {
        MusicEnabled = enabled;
        
        var audioManager = Engine.Instance.GetBrowserAudioManager();
        if (audioManager.Contains("music"))
        {
            var music = audioManager.Get("music");
            if (enabled)
                music.Play(fromStart: false);
            else
                music.Pause();
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
                StopPlayerJiggle(player);
        }
    }

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
        Engine.Managers.DirectDrawings.ClearAll();
        Engine.Managers.Sprites.Clear();
        Scene!.RemoveAllLayers();

        SetPlayerFrames(options.Players);

        var newGameResult = SpotGame.NewGame(options.BoardWidth, options.BoardHeight, options.Players.ToArray());

        Scene!.AddLayer(newGameResult.Field);
        Scene!.AddLayer(newGameResult.BackgroundField);

        CreateTextBlockFields();
    }

    #endregion game settings

    #region private helpers

    private void MouseEventPoller_MouseEvent(Gondwana.Input.Mouse.MouseEventArgs args)
    {
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
                    player.DefaultFrame = new Frame(_spotSheetDefault, 0, 0);
                    player.ActiveFrame  = new Frame(_spotSheetSelected, 0, 0);
                    break;
                case "Green":
                    player.DefaultFrame = new Frame(_spotSheetDefault, 0, 1);
                    player.ActiveFrame  = new Frame(_spotSheetSelected, 1, 0);
                    break;
                case "Violet":
                    player.DefaultFrame = new Frame(_spotSheetDefault, 0, 2);
                    player.ActiveFrame  = new Frame(_spotSheetSelected, 2, 0);
                    break;
                case "Red":
                    player.DefaultFrame = new Frame(_spotSheetDefault, 0, 3);
                    player.ActiveFrame  = new Frame(_spotSheetSelected, 3, 0);
                    break;
                case "Yellow":
                    player.DefaultFrame = new Frame(_spotSheetDefault, 0, 4);
                    player.ActiveFrame  = new Frame(_spotSheetSelected, 4, 0);
                    break;
            }
        }
    }

    private void StartPlayerJiggle(Player player)
    {
        if (JiggleEnabled)
        {
            foreach (var cell in SpotGame.SpotGameField.GetAllCellsForPlayer(player))
                cell.Sprite?.StartJiggle(loop: true);
        }
    }

    private void StopPlayerJiggle(Player player)
    {
        foreach (var cell in SpotGame.SpotGameField.GetAllCellsForPlayer(player))
            cell.Sprite?.StopJiggle();
    }

    private void JiggleAllPlayers()
    {
        foreach (var player in SpotGame.Players)
            StartPlayerJiggle(player);
    }

    private void PlaySound(string key)
    {
        if (!SoundEffectsEnabled) return;
        
        var audioManager = Engine.Instance.GetBrowserAudioManager();
        if (audioManager.Contains(key))
        {
            audioManager.Get(key).Play(fromStart: true);
        }
    }

    #endregion private helpers

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
            Position      = new PointF(width * 1.1f, height * 0.5f),
            JitterY       = height * 0.5f,
            EmitRate      = 0.65f,
            LifeRange     = (1000f, 2000f),
            VelocityRangeX = (-100f, -50f),
            VelocityRangeY = (-1f, 1f),
            SizeRange     = (40f, 80f),
            GravityY      = 0f,
            BlendMode     = SKBlendMode.SrcOver,
            OnSpawn = (ref Particle p) =>
            {
                var baseColor = colors[_rng.Next(colors.Length)];
                p.Color = baseColor.WithAlpha(255);
            }
        };
    }

    private void AddClouds()
    {
        _particleSurface?.Dispose();
        _particleSurface = null;

        if (SpotGame?.BackgroundGameField is null)
            return;

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
            Position       = new PointF(width * 1.4f, height * 0.5f),
            JitterY        = height * 0.5f,
            EmitRate       = 0.075f,
            LifeRange      = (2000f, 2000f),
            VelocityRangeX = (-50f, -25f),
            VelocityRangeY = (-1f, 1f),
            SizeRange      = (200f, 500f),
            GravityY       = 0f,
            BlendMode      = SKBlendMode.SrcOver,
            ParticleSprite = _clouds?.SkBitmap,
            OnSpawn = (ref Particle p) =>
            {
                p.AngularVel = 0;
                p.Rotation   = 0;
                byte alpha   = (byte)_rng.Next(100, 180);
                p.Tint       = new SKColor(255, 255, 255, alpha);
            }
        };
    }

    #endregion particle emitters

    #region score display

    private void CreateTextBlockFields()
    {
        if (Scene is null || RenderSurface.Host.ViewManager.Views.Count == 0 || SpotGame is null)
            return;

        var view = RenderSurface.Host.ViewManager.Views[0];
        int w = RenderSurface.Host.Backbuffer.Width;
        int h = RenderSurface.Host.Backbuffer.Height;

        _player1Text = new TextBlock(RenderSurface.Host, view, new Rectangle(10, 10, 200, 50));
        _player1Text.SetFont(_font, 24, 12)
                    .SetColors(SpotGame.Players[0].ColorItem.TextColor, SKColors.Transparent)
                    .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                    .SetText(SpotGame.Players[0].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[0]))
                    .SetMaxLines(1)
                    .UseShadow()
                    .SetShadow(3, 3, 200, 3.0f);
        _player1Text.ZOrder = 20;

        _player1Rectangle = new DirectRectangle(SpotGame.Players[0].ColorItem.Color.ToColor(),
                                                RenderSurface.Host, view, _player1Text.ScreenBounds);
        _player1Rectangle.SetCornerRadius(30).SetFilled(true);

        _player2Text = new TextBlock(RenderSurface.Host, view, new Rectangle(w - 210, h - 60, 200, 50));
        _player2Text.SetFont(_font, 24, 12)
                    .SetColors(SpotGame.Players[1].ColorItem.TextColor, SKColors.Transparent)
                    .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                    .SetText(SpotGame.Players[1].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[1]))
                    .SetMaxLines(1)
                    .UseShadow()
                    .SetShadow(3, 3, 200, 3.0f);
        _player2Text.ZOrder = 20;

        _player2Rectangle = new DirectRectangle(SpotGame.Players[1].ColorItem.Color.ToColor(),
                                                RenderSurface.Host, view, _player2Text.ScreenBounds);
        _player2Rectangle.SetCornerRadius(30).SetFilled(true);

        if (SpotGame.Players.Length >= 3)
        {
            _player3Text = new TextBlock(RenderSurface.Host, view, new Rectangle(w - 210, 10, 200, 50));
            _player3Text.SetFont(_font, 24, 12)
                        .SetColors(SpotGame.Players[2].ColorItem.TextColor, SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                        .SetText(SpotGame.Players[2].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[2]))
                        .SetMaxLines(1)
                        .UseShadow()
                        .SetShadow(3, 3, 200, 3.0f);
            _player3Text.ZOrder = 20;

            _player3Rectangle = new DirectRectangle(SpotGame.Players[2].ColorItem.Color.ToColor(),
                                                    RenderSurface.Host, view, _player3Text.ScreenBounds);
            _player3Rectangle.SetCornerRadius(30).SetFilled(true);
        }

        if (SpotGame.Players.Length >= 4)
        {
            _player4Text = new TextBlock(RenderSurface.Host, view, new Rectangle(10, h - 60, 200, 50));
            _player4Text.SetFont(_font, 24, 12)
                        .SetColors(SpotGame.Players[3].ColorItem.TextColor, SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                        .SetText(SpotGame.Players[3].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[3]))
                        .SetMaxLines(1)
                        .UseShadow()
                        .SetShadow(3, 3, 200, 3.0f);
            _player4Text.ZOrder = 20;

            _player4Rectangle = new DirectRectangle(SpotGame.Players[3].ColorItem.Color.ToColor(),
                                                    RenderSurface.Host, view, _player4Text.ScreenBounds);
            _player4Rectangle.SetCornerRadius(30).SetFilled(true);
        }

        if (SpotGame.SpotGameField.GridColumnCount > 10 || SpotGame.SpotGameField.GridRowCount > 10)
            SetScoreVisible(false);
    }

    private void SetScoreVisible(bool visible)
    {
        _showScores = visible;

        if (_player1Text is not null) { _player1Text.Visible = visible; _player1Rectangle!.Visible = visible; }
        if (_player2Text is not null) { _player2Text.Visible = visible; _player2Rectangle!.Visible = visible; }
        if (_player3Text is not null) { _player3Text.Visible = visible; _player3Rectangle!.Visible = visible; }
        if (_player4Text is not null) { _player4Text.Visible = visible; _player4Rectangle!.Visible = visible; }

        if (visible) SetPlayerScores();
    }

    private void SetPlayerScores()
    {
        _player1Text?.SetText(SpotGame.Players[0].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[0]));
        _player2Text?.SetText(SpotGame.Players[1].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[1]));
        if (SpotGame.Players.Length >= 3) _player3Text?.SetText(SpotGame.Players[2].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[2]));
        if (SpotGame.Players.Length >= 4) _player4Text?.SetText(SpotGame.Players[3].Name + " - " + SpotGame.GetPlayerScore(SpotGame.Players[3]));
    }

    private void CreateGameOverText(List<Player> winningPlayers)
    {
        bool multipleWinners;
        string message;

        var primaryTextColor  = winningPlayers[0].ColorItem.TextColor.ToColor();
        Color? secondaryTextColor  = null;
        var primaryFillColor  = winningPlayers[0].ColorItem.Color.ToColor();
        Color? secondaryFillColor  = null;

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

        if (Scene is null || RenderSurface.Host.ViewManager.Views.Count == 0) return;
        var view = RenderSurface.Host.ViewManager.Views[0];
        int w = RenderSurface.Host.Backbuffer.Width;
        int h = RenderSurface.Host.Backbuffer.Height;

        _gameMessageText = new TextBlock(RenderSurface.Host, view,
            new Rectangle(w / 2 - 180, h / 2 - 40, 360, 80));
        _gameMessageText.SetFont(_font, 48, 16)
                        .SetColors(primaryTextColor.ToSKColor(), SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                        .SetText(message)
                        .UseShadow()
                        .SetShadow(5, 5, 200, 3.0f)
                        .EnableWrapping();
        _gameMessageText.ZOrder = 20;

        _gameMessageRectangle = new DirectRectangle(primaryFillColor, RenderSurface.Host, view,
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
        SpotGame.GameStarted              += OnGameStarted;
        SpotGame.PlayerTurnStarted        += OnPlayerTurnStarted;
        SpotGame.PlayerTurnEnded          += OnPlayerTurnEnded;
        SpotGame.SpotSelected             += OnSpotSelected;
        SpotGame.SpotDeselected           += OnSpotDeselected;
        SpotGame.InvalidSelectionAttempted += OnInvalidSelectionAttempted;
        SpotGame.InvalidMoveAttempted      += OnInvalidMoveAttempted;
        SpotGame.PlayerMoveStarted        += OnPlayerMoveStarted;
        SpotGame.PlayerMoveStopped        += OnPlayerMoveStopped;
        SpotGame.CellsCaptured            += OnCellsCaptured;
        SpotGame.NoValidMovesAvailable    += OnNoValidMovesAvailable;
        SpotGame.GameOver                 += OnGameOver;
    }

    private void UnhookSpotGameEvents()
    {
        if (SpotGame is null) return;
        SpotGame.GameStarted              -= OnGameStarted;
        SpotGame.PlayerTurnStarted        -= OnPlayerTurnStarted;
        SpotGame.PlayerTurnEnded          -= OnPlayerTurnEnded;
        SpotGame.SpotSelected             -= OnSpotSelected;
        SpotGame.SpotDeselected           -= OnSpotDeselected;
        SpotGame.InvalidSelectionAttempted -= OnInvalidSelectionAttempted;
        SpotGame.InvalidMoveAttempted      -= OnInvalidMoveAttempted;
        SpotGame.PlayerMoveStarted        -= OnPlayerMoveStarted;
        SpotGame.PlayerMoveStopped        -= OnPlayerMoveStopped;
        SpotGame.CellsCaptured            -= OnCellsCaptured;
        SpotGame.NoValidMovesAvailable    -= OnNoValidMovesAvailable;
        SpotGame.GameOver                 -= OnGameOver;
    }

    private void OnGameStarted(SpotGame game)
    {
        if (CloudsEnabled) AddClouds();
    }

    private void OnPlayerTurnStarted(Player player)
    {
        StartPlayerJiggle(player);

        if (player.Type == PlayerType.Human)
        {
            _handleHumanInput = true;
        }
        else
        {
            _handleHumanInput = false;

            var timer = Gondwana.Timers.Timer.Add(TimerType.PostCycle, TimerCycles.Once, 0.6);
            timer.Tick += () =>
            {
                timer.Dispose();
                var moves    = SpotGame.SpotGameField.GetBestMovesForPlayer(player);
                var bestMove = moves[_rng.Next(moves.Count)];
                SpotGame.AttemptSelectCell(bestMove.FromCell, out _);

                var moveTimer = Gondwana.Timers.Timer.Add(TimerType.PostCycle, TimerCycles.Once, 0.6);
                moveTimer.Tick += () =>
                {
                    moveTimer.Dispose();
                    SpotGame.ExecuteMove(bestMove);
                };
            };
        }
    }

    private void OnPlayerTurnEnded(Player player)
    {
        StopPlayerJiggle(player);
    }

    private void OnSpotSelected(SpotGameField.Cell cell)
    {
        var sprite = cell.Sprite!;
        sprite.StopJiggle();
        sprite.CurrentFrame = cell.OccupiedBy!.ActiveFrame;
        sprite.PulseBy(1.1f, 0.4f, 0.4f, true);
        
        PlaySound("spotSelected");
    }

    private void OnSpotDeselected(SpotGameField.Cell cell)
    {
        var sprite = cell.Sprite!;
        sprite.StartJiggle(loop: true);
        sprite.CurrentFrame = cell.OccupiedBy!.DefaultFrame;
        sprite.StopPulse(true, 0.2f);
        
        PlaySound("spotDeselected");
    }

    private void OnInvalidSelectionAttempted(SpotGameField.Cell cell)
    {
        PlaySound("bump");
    }

    private void OnInvalidMoveAttempted(SpotGameField.Cell cell)
    {
        PlaySound("bump");
    }

    private void OnPlayerMoveStarted(PlayerMovement movement)
    {
        PlaySound("velcro");
    }

    private void OnPlayerMoveStopped(PlayerMovement movement)
    {
        if (_showScores) SetPlayerScores();
        SpotGame.NextPlayer();
        
        PlaySound("drop");
    }

    private void OnCellsCaptured(List<SpotGameField.Cell> cellsCaptured)
    {
        foreach (var cell in cellsCaptured)
        {
            var oldSprite = cell.Sprite;
            if (oldSprite == null) continue;

            Action? handler = null;
            handler = () =>
            {
                oldSprite.ResizeComplete -= handler;
                oldSprite.CurrentFrame   = cell.OccupiedBy!.DefaultFrame;
                oldSprite.ResizeTo(new(56, 56), 0.2f);
            };
            oldSprite.ResizeComplete += handler;
            oldSprite.ResizeTo(new(1, 1), 0.2f);
        }
        
        if (cellsCaptured.Count > 0)
        {
            PlaySound("knock");
        }
    }

    private void OnNoValidMovesAvailable(Player player)
    {
        SpotGame.NextPlayer();
    }

    private void OnGameOver()
    {
        _handleHumanInput = false;
        SetScoreVisible(true);
        SetPlayerScores();
        StopPlayerJiggle(SpotGame.CurrentPlayer);
        JiggleAllPlayers();

        var allScores    = SpotGame.GetAllPlayerScores();
        var maxScore     = allScores.Values.Max();
        var winners      = allScores.Where(kvp => kvp.Value == maxScore).Select(kvp => kvp.Key).ToList();

        CreateGameOverText(winners);
        
        // Play win or lose sound
        bool currentPlayerWon = winners.Contains(SpotGame.CurrentPlayer);
        PlaySound(currentPlayerWon ? "gameWin" : "gameLose");
    }

    #endregion SpotGame event handlers
}
