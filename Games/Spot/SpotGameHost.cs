using Gondwana;
using Gondwana.Audio;
using Gondwana.Audio.Midi;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Direct.Particles;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using SkiaSharp;
using System;
using System.Drawing;

namespace HWG.Spot;

public sealed class SpotGameHost : WinFormsGameHost
{
    public AudioResource _dorian;
    public AudioResource _tada;
    public AudioResource _music;

    public SpotGameHost(WinFormBitmapRenderSurfaceControl renderSurface)
        : base(renderSurface) { }

    protected override void LoadAssets()
    {
        // load asset files
        //_dorian = AudioResourceManager.Instance.LoadFromFile("dorian", "assets\\dorian.mid");
        //_dorian.IsLooping = true;

        _tada = AudioResourceManager.Instance.LoadFromFile("tada", "assets\\tada.wav");
        _tada.IsLooping = false;

        _music = AudioResourceManager.Instance.LoadFromFile("music", "assets\\sounovamusic-puzzle-amp-casual-game-music-460543.mp3");
        _music.IsLooping = true;

        // load standalone audio files

        // load standalone image files

        // load standalone video files

        // load standalone cursor files
    }

    protected override void LoadTilesheets()
    {
        // Implementation for loading tilesheets goes here
        var splash = new Tilesheet("splash", "assets\\spot.png");
        splash.ApplyMask(Color.Black.ToSKColor());

        var clouds = new Tilesheet("clouds", "assets\\clouds_slice.png");
        clouds.ApplyMask(Color.Black.ToSKColor());
    }

    protected override void LoadAnimationCycles()
    {
        // Implementation for loading animation cycles goes here
    }

    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();

        var sceneLayer1 = scene.AddLayer(
            columnCount: 12,
            rowCount: 12,
            width: 64,
            height: 64,
            zOrder: 10,
            parallax: 1f,
            coordinateSystem: CoordinateSystemTypes.Orthogonal);

        sceneLayer1.ShowGridLines = false;

        // Example:
        // var sourceTilesheet = TilesheetRegistry.Instance.GetAll()["tiles"];
        // sceneLayer1[0, 0] = new Tile(sourceTilesheet, 0);

        return scene;
    }

    protected override void CreateSceneGraph()
    {
        base.CreateSceneGraph();
        RenderSurface.Host.Backbuffer.ClearColor = Color.CornflowerBlue.ToSKColor();
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
            var directImage = new DirectImage(tilesheet.SkBitmap, RenderSurface.Host, Scene[0], new Rectangle(0, 0, 769, 769));
            directImage.ZOrder = 100;
            directImage.SetScaleMode(DirectImage.ScaleMode.Fit);
        }

        var particleSurface = new ParticleSurface(RenderSurface.Host, Scene[0], new Rectangle(0, 0, 769, 769));
        particleSurface.CullingMarginX = 1300f;
        particleSurface.ZOrder = 50;
        var spriteBmp = tilesheet.SkBitmap;
        particleSurface.Emitters.Add(GetSpots(769, 769));
    }

    private static readonly Random _rng = new();

    private ParticleEmitter GetSpots(float width, float height)
    {
        SKColor[] colors =
        {
            SKColors.White,
            SKColors.Red,
            SKColors.Blue,
            SKColors.Yellow,
            SKColors.Green,
            //SKColors.Purple
        };

        return new ParticleEmitter
        {
            Position = new PointF(width * 1.1f, height * 0.5f),
            JitterY = height * 0.5f,

            EmitRate = 0.5f,
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
        //_dorian.Volume = 1f;
        //_dorian.Play();
        //_tada.Play();
        _music.Volume = 0.2f;
        _music.Play();
    }

    protected override void OnConfigurePlatform()
    {
        Engine.InitializeMidiAudioFormats();
    }

    protected override void OnMouseAdapterInitialized()
    {
        if (Engine.MouseEventPoller is null)
            return;

        Engine.MouseEventPoller.MouseEvent += MouseEventPoller_MouseEvent;
        Engine.MouseEventPoller.StartMonitoringMouse();
    }

    protected override void UnhookEvents()
    {
        if (Engine.MouseEventPoller is not null)
            Engine.MouseEventPoller.MouseEvent -= MouseEventPoller_MouseEvent;
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
}
