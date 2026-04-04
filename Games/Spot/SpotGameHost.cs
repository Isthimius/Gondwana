using Gondwana;
using Gondwana.Audio;
using Gondwana.Audio.Midi;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Direct.Particles;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using HWG.Spot.Game;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace HWG.Spot;

internal sealed class SpotGameHost : WinFormsGameHost
{
    internal AudioResource _music;

    internal AudioResource _spotSelected;
    internal AudioResource _velcro;
    internal AudioResource _drop;
    internal AudioResource _gameWin;
    internal AudioResource _gameLose;
    internal AudioResource _bump;
    internal AudioResource _knock;
    internal AudioResource _spotCaptured;

    internal Tilesheet _blueSpot;
    internal Tilesheet _greenSpot;
    internal Tilesheet _pinkSpot;
    internal Tilesheet _redSpot;
    internal Tilesheet _yellowSpot;
    internal Tilesheet _blueSpotHappy;
    internal Tilesheet _greenSpotHappy;
    internal Tilesheet _pinkSpotHappy;
    internal Tilesheet _redSpotHappy;
    internal Tilesheet _yellowSpotHappy;

    internal SKTypeface _font;

    //internal int ScoreHeight = 80;

    internal SpotGame SpotGame { get; private set; }

    private static readonly Random _rng = new();

    internal SpotGameHost(WinFormBitmapRenderSurfaceControl renderSurface)
        : base(renderSurface) { }

    #region overrides

    protected override void LoadAssets()
    {
        // load asset files

        // load standalone audio files
        _music = Engine.Managers.AudioResources.LoadFromFile("music", "assets\\sounovamusic-puzzle-amp-casual-game-music-460543.mp3");
        _music.IsLooping = true;

        //_spotSelected = gotta find it
        _velcro = Engine.Managers.AudioResources.LoadFromFile("velcro", "assets\\freesound_community-velcro_fast-91558.mp3");
        _drop = Engine.Managers.AudioResources.LoadFromFile("drop", "assets\\freesound_community-water-drip-45622.mp3");
        _gameWin = Engine.Managers.AudioResources.LoadFromFile("gameWin", "assets\\peekaboolabcreative-11l-victory_sound_with_t-1749487402950-357606.mp3");
        _gameLose = Engine.Managers.AudioResources.LoadFromFile("gameLose", "assets\\freesound_community-080047_lose_funny_retro_video-game-80925.mp3");
        _bump = Engine.Managers.AudioResources.LoadFromFile("bump", "assets\\freesound_community-bump-7-92964.mp3");
        //_knock = gotta find it
        //_spotCaptured = gotta find it

        // load standalone video files

        // load standalone font files
        _font = Engine.Managers.Fonts.LoadFromFile("main", "assets\\ArchitectsDaughter-Regular.ttf");

        // load standalone cursor files
    }

    protected override void LoadTilesheets()
    {
        // splash logo
        var splash = new Tilesheet("splash", "assets\\spot.png");
        splash.ApplyMask(Color.Black.ToSKColor());

        // defautl sprites
        _blueSpot = new Tilesheet("blueSpot", "assets\\bubble-blue.png");
        _blueSpot.TileSize = new Size(92, 96);

        _greenSpot = new Tilesheet("greenSpot", "assets\\bubble-green.png");
        _greenSpot.TileSize = new Size(92, 96);

        _pinkSpot = new Tilesheet("pinkSpot", "assets\\bubble-pink.png");
        _pinkSpot.TileSize = new Size(92, 96);

        _redSpot = new Tilesheet("redSpot", "assets\\bubble-red.png");
        _redSpot.TileSize = new Size(92, 96);

        _yellowSpot = new Tilesheet("yellowSpot", "assets\\bubble-yellow.png");
        _yellowSpot.TileSize = new Size(92, 96);

        // selected sprites
        _blueSpotHappy = new Tilesheet("blueSpotHappy", "assets\\bubble-blue-happy.png");
        _blueSpotHappy.TileSize = new Size(1024, 1024);

        _greenSpotHappy = new Tilesheet("greenSpotHappy", "assets\\bubble-green-happy.png");
        _greenSpotHappy.TileSize = new Size(1024, 1024);

        _pinkSpotHappy = new Tilesheet("pinkSpotHappy", "assets\\bubble-pink-happy.png");
        _pinkSpotHappy.TileSize = new Size(1024, 1024);

        _redSpotHappy = new Tilesheet("redSpotHappy", "assets\\bubble-red-happy.png");
        _redSpotHappy.TileSize = new Size(1024, 1024);

        _yellowSpotHappy = new Tilesheet("yellowSpotHappy", "assets\\bubble-yellow-happy.png");
        _yellowSpotHappy.TileSize = new Size(1024, 1024);
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
    }

    protected override void OnStartEngine()
    {
        _music.Volume = 0.2f;
        _music.Play();
    }

    protected override void OnConfigurePlatform()
    {
        Engine.InitializeMidiAudioFormats();
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

    #endregion overrides

    private ParticleEmitter GetSpots(float width, float height)
    {
        SKColor[] colors =
        {
            //SKColors.White,
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

    private void MouseEventPoller_MouseEvent(Gondwana.Input.Mouse.MouseEventArgs args)
    {
        if (Scene is null || Scene.SceneLayers.Count == 0)
            return;

        if (RenderSurface.Host.ViewManager.Views.Count == 0)
            return;

        var view = RenderSurface.Host.ViewManager.Views[0];
        var layer = Scene.SceneLayers[0];

        var screenPos = args.CurrentPosition;

        if (args.ButtonStates.First(s => s.Key == Gondwana.Input.Mouse.MouseButton.Left).Value.JustPressed)
        {
            var selectedCoord = view.ScreenPxToGrid(layer, screenPos);

            if (selectedCoord.X >= 0 && selectedCoord.X < layer.GridColumnCount &&
                selectedCoord.Y >= 0 && selectedCoord.Y < layer.GridRowCount)
            {
                var cell = SpotGame.SpotGameField.GetCell((int)selectedCoord.X, (int)selectedCoord.Y);

                if (SpotGame.AttemptSelectCell(cell, out var playerMovement))
                {
                    if (playerMovement != null)
                        SpotGame.ExecuteMove(playerMovement.Value);
                }
            }
        }
    }

    internal void SetMusicEnabled(bool enabled)
    {
        MusicEnabled = enabled;

        if (enabled)
            _music.Play();
        else
            _music.Stop();
    }

    internal bool MusicEnabled { get; private set; } = true;

    internal bool SoundEffectsEnabled { get; private set; } = true;

    internal void SetSoundEffectsEnabled(bool enabled)
    {
        SoundEffectsEnabled = enabled;
    }

    internal void StartNewGame(NewGameOptions options)
    {
        Engine.Managers.DirectDrawings.ClearAll();
        Engine.Managers.Sprites.Clear();
        Scene.RemoveAllLayers();

        SetPlayerFrames(options.Players);

        var newGameResult = SpotGame.NewGame(options.BoardWidth, options.BoardHeight, options.Players.ToArray());

        Scene.AddLayer(newGameResult.Field);
        Scene.AddLayer(newGameResult.BackgroundField);
        _music.Volume = 0.1f;
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
        SpotGame.PlayerMoved += OnPlayerMoved;
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
        SpotGame.PlayerMoved -= OnPlayerMoved;
        SpotGame.CellsCaptured -= OnCellsCaptured;
        SpotGame.NoValidMovesAvailable -= OnNoValidMovesAvailable;
        SpotGame.GameOver -= OnGameOver;
    }

    private void OnGameStarted(SpotGame game)
    {
        if (MusicEnabled)
        {
            if (!_music.IsPlaying) 
                _music.Play();
        }
    }

    private void OnPlayerTurnStarted(Player player)
    {
        foreach (var cell in SpotGame.SpotGameField.GetAllCellsForPlayer(player))
        {
            cell.Sprite.StartJiggle(loop: true);
        }
    }

    private void OnPlayerTurnEnded(Player player)
    {
        foreach (var cell in SpotGame.SpotGameField.GetAllCellsForPlayer(player))
        {
            cell.Sprite.StopJiggle();
        }
    }

    private void OnSpotSelected(SpotGameField.Cell cell)
    {
        if (SoundEffectsEnabled)
            _spotSelected?.Play();

        cell.Sprite.StopJiggle();
        cell.Sprite.CurrentFrame = cell.OccupiedBy.ActiveFrame;
        cell.Sprite.PulseBy(1.1f, 0.4f, 0.4f, true);
    }

    private void OnSpotDeselected(SpotGameField.Cell cell)
    {
        cell.Sprite.StartJiggle(loop: true);
        cell.Sprite.CurrentFrame = cell.OccupiedBy.DefaultFrame;
        cell.Sprite.StopPulse(true, 0.2f);
    }

    private void OnInvalidSelectionAttempted(SpotGameField.Cell cell)
    {
        if (SoundEffectsEnabled)
            _bump?.Play();
    }

    private void OnInvalidMoveAttempted(SpotGameField.Cell cell)
    {
        if (SoundEffectsEnabled)
            _knock?.Play();
    }

    private void OnPlayerMoved(PlayerMovement movement)
    {
        if (movement.MovementType == MovementType.Jump)
        {
            if (SoundEffectsEnabled)
                _velcro?.Play();
        }

        if (movement.MovementType == MovementType.Clone)
        {
            if (SoundEffectsEnabled)
                _drop?.Play();
        }

        SpotGame.NextPlayer();
    }

    private void OnCellsCaptured(List<SpotGameField.Cell> cells)
    {
        if (SoundEffectsEnabled)
            _spotCaptured?.Play();
    }

    private void OnNoValidMovesAvailable(Player player)
    {
        if (SoundEffectsEnabled)
            _bump?.Play();
    }

    private void OnGameOver()
    {
        var winner = SpotGame.Players.OrderByDescending(p => SpotGame.GetPlayerScore(p)).FirstOrDefault();

        if (MusicEnabled)
        {
            _music?.Stop();

            if (winner.Type == PlayerType.Human)
                _gameWin?.Play();
            else
                _gameLose?.Play();
        }
    }

    #endregion SpotGame event handlers
}