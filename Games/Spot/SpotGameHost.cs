using Gondwana;
using Gondwana.Audio;
using Gondwana.Audio.Midi;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Direct.Particles;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Rendering.Text;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using SkiaSharp;
using Spot.Game;
using System;
using System.Drawing;

namespace HWG.Spot;

internal sealed class SpotGameHost : WinFormsGameHost
{
    internal AudioResource _music;
    internal AudioResource _velcro;
    internal AudioResource _drop;
    internal AudioResource _gameWin;
    internal AudioResource _gameLose;
    internal AudioResource _bump;
    internal SKTypeface _font;

    internal int ScoreHeight = 80;

    internal SpotGame SpotGame { get; private set; }

    internal SpotGameHost(WinFormBitmapRenderSurfaceControl renderSurface)
        : base(renderSurface) { }

    protected override void LoadAssets()
    {
        // load asset files

        // load standalone audio files
        _music = AudioResourceManager.Instance.LoadFromFile("music", "assets\\sounovamusic-puzzle-amp-casual-game-music-460543.mp3");
        _music.IsLooping = true;

        _velcro = AudioResourceManager.Instance.LoadFromFile("velcro", "assets\\freesound_community-velcro_fast-91558.mp3");
        _drop = AudioResourceManager.Instance.LoadFromFile("drop", "assets\\freesound_community-water-drip-45622.mp3");
        _gameWin = AudioResourceManager.Instance.LoadFromFile("gameWin", "assets\\peekaboolabcreative-11l-victory_sound_with_t-1749487402950-357606.mp3");
        _gameLose = AudioResourceManager.Instance.LoadFromFile("gameLose", "assets\\freesound_community-080047_lose_funny_retro_video-game-80925.mp3");
        _bump = AudioResourceManager.Instance.LoadFromFile("bump", "assets\\freesound_community-bump-7-92964.mp3");

        // load standalone image files

        // load standalone video files

        // load standalone font files
        _font = FontManager.Instance.LoadFromFile("main", "assets\\ArchitectsDaughter-Regular.ttf");

        // load standalone cursor files
    }

    protected override void LoadTilesheets()
    {
        // Implementation for loading tilesheets goes here
        var splash = new Tilesheet("splash", "assets\\spot.png");
        splash.ApplyMask(Color.Black.ToSKColor());
    }

    protected override void LoadAnimationCycles()
    {
        // Implementation for loading animation cycles goes here
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
        SpotGame = new SpotGame(this);
    }

    protected override void CreateSprites()
    {
        // Implementation for creating sprites goes here
    }

    protected override void CreateDirectDrawings()
    {
        Tilesheet tilesheet;

        if (TilesheetRegistry.Instance.TryGet("splash", out tilesheet))
        {
            var directImage = new DirectImage(tilesheet.SkBitmap, RenderSurface.Host, Scene[0], new Rectangle(0, 0, 769, 769 + ScoreHeight));
            directImage.ZOrder = 100;
            directImage.SetScaleMode(DirectImage.ScaleMode.Fit);
        }

        var particleSurface = new ParticleSurface(RenderSurface.Host, Scene[0], new Rectangle(0, 0, 769, 769 + ScoreHeight));
        particleSurface.CullingMarginX = 1300f;
        particleSurface.ZOrder = 50;
        var spriteBmp = tilesheet.SkBitmap;
        particleSurface.Emitters.Add(GetSpots(769, 769 + ScoreHeight));
    }

    private static readonly Random _rng = new();

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

                // keep the same transparency you had before
                p.Color = baseColor.WithAlpha(255);
            }
        };
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

        // 1) screen -> world
        var worldFromScreen = view.ScreenPxToWorldPx(layer, screenPos);

        // 2) screen -> grid
        var gridFromScreen = view.ScreenPxToGrid(layer, screenPos);

        // 3) grid -> world
        var worldFromGrid = layer.GridToWorldPx(gridFromScreen);

        // 4) world -> screen
        var screenFromGrid = view.WorldPxToScreenPx(layer, worldFromGrid);

        _ = worldFromScreen;
        _ = gridFromScreen;
        _ = worldFromGrid;
        _ = screenFromGrid;
    }

    internal void SetMusicEnabled(bool enabled)
    {
        if (enabled)
            _music.Play();
        else
            _music.Stop();
    }

    internal void SetSoundEffectsEnabled(bool enabled)
    {
        // AudioManager.SoundEffectsEnabled = enabled;
    }
}
