using System;
using System.Drawing;
using System.IO;
using Gondwana.Audio;
using Gondwana.Demos.Spot.Game;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using Gondwana.Widgets.Overlays;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Gondwana.Demos.Spot;

internal sealed partial class SpotGameHost
{
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

    internal SplashScreen? CreateSplash(Gondwana.Rendering.RenderSurfaceHostBase host, Action onSplashCompleted)
    {
        var imagePath = Path.Combine(AppContext.BaseDirectory, "assets", "gondwana-logo-text.png");
        using var imageStream = File.OpenRead(imagePath);
        var view = host.ViewManager.Views[0];

        var splash = SplashScreen.TryCreate(imageStream: imageStream,
                                            host: host,
                                            view: view,
                                            onSplashCompleted: onSplashCompleted);

        // Preserve the source aspect ratio while covering the taller client area.
        // The small amount of cropping is preferable to letterboxing above/below the splash.
        splash?.Image.SetScaleMode(DirectImage.ScaleMode.Fill);
        return splash;
    }

    protected override void LoadAssets()
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

    protected override void LoadTilesheets()
    {
        // splash logo
        var splash = Engine.Managers.Tilesheets.LoadFromImageFile("splash", "assets\\spot.png");
        splash.ApplyMask(Color.Black.ToSKColor());

        _spotSheetDefault = Engine.Managers.Tilesheets.LoadFromImageFile("spots", "assets\\spot_defaults.png");
        _spotSheetDefault.DefaultRegion.TileSize = new Size(93, 96);

        _spotSheetSelected = Engine.Managers.Tilesheets.LoadFromImageFile("selected", "assets\\spot_selected.png");
        _spotSheetSelected.DefaultRegion.TileSize = new Size(64, 64);

        _clouds = Engine.Managers.Tilesheets.LoadFromImageFile("clouds", "assets\\clouds.png");
    }

    protected override Scene CreateInitialScene()
    {
        Logging.EngineLogger.SetLogLevel(LogLevel.Information);
        Gondwana.Engine.Instance.CPSCalculated += (args) =>
        {
            if (SurfaceHost.Backbuffer is GpuBackbuffer gpuBackbuffer)
            {
                string gpuFps = args.GpuFps.HasValue
                    ? args.GpuFps.Value.ToString("0.0")
                    : "n/a";

                Engine.Logger.LogInformation(
                    "CPS {Cps:0.0} | engine FPS {EngineFps:0.0} | GPU FPS {GpuFps} | " +
                    "MSAA requested {MsaaSampleCount} | MSAA actual {ActualMsaaSampleCount} | " +
                    "MSAA max {MaxSupportedMsaaSampleCount}",
                    args.GrossCPS,
                    args.NetCPS,
                    gpuFps,
                    gpuBackbuffer.MsaaSampleCount,
                    gpuBackbuffer.ActualMsaaSampleCount,
                    gpuBackbuffer.MaxSupportedMsaaSampleCount);

                return;
            }

            Engine.Logger.LogInformation("{CyclesPerSecond}", args);
        };

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
        sceneLayer1.OriginPx = new Point(0, -PersistentMenuHeight);

        return scene;
    }

    protected override void OnSceneGraphCreated()
    {
        SurfaceHost.Backbuffer.ClearColor = Color.CornflowerBlue.ToSKColor();

        SpotGame = new SpotGame();
        HookSpotGameEvents();
    }
}
