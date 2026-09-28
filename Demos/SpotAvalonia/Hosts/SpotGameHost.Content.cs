using System.Drawing;
using Gondwana.Audio;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Tilesheets;
using Gondwana.SkiaSharp;
using Gondwana.Widgets.Overlays;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Gondwana.Demos.Spot;

internal sealed partial class SpotGameHost
{
    private AudioResource? _music;
    private AudioResource? _spotSelected;
    private AudioResource? _spotDeselected;
    private AudioResource? _velcro;
    private AudioResource? _drop;
    private AudioResource? _gameWin;
    private AudioResource? _gameLose;
    private AudioResource? _bump;
    private AudioResource? _knock;

    private Tilesheet _spotSheetDefault = null!;
    private Tilesheet _spotSheetSelected = null!;
    private Tilesheet _clouds = null!;
    private SKTypeface _font = null!;

    internal SplashScreen? CreateSplash(RenderSurfaceHostBase host, Action onSplashCompleted)
    {
        using var imageStream = File.OpenRead(GetAssetPath("gondwana-logo-text.png"));
        var view = host.ViewManager.Views[0];

        var splash = SplashScreen.TryCreate(
            imageStream: imageStream,
            host: host,
            view: view,
            onSplashCompleted: onSplashCompleted);

        splash?.Image.SetScaleMode(DirectImage.ScaleMode.Fill);
        return splash;
    }

    protected override void LoadAssets()
    {
        if (Engine.Managers.AudioResources.IsBackendConfigured)
        {
            _music = LoadAudio("music", "sounovamusic-puzzle-amp-casual-game-music-460543.mp3");
            _music.IsLooping = true;

            _spotSelected = LoadAudio("spotSelected", "universfield-bubble-pop-293342.mp3");
            _spotSelected.Volume = 0.4f;

            _spotDeselected = LoadAudio("spotDeselected", "universfield-bubble-pop-293342.mp3");
            _spotDeselected.Volume = 0.15f;

            _velcro = LoadAudio("velcro", "freesound_community-velcro_fast-91558.mp3");
            _drop = LoadAudio("drop", "freesound_community-water-drip-45622.mp3");
            _gameWin = LoadAudio("gameWin", "peekaboolabcreative-11l-victory_sound_with_t-1749487402950-357606.mp3");
            _gameLose = LoadAudio("gameLose", "freesound_community-080047_lose_funny_retro_video-game-80925.mp3");
            _bump = LoadAudio("bump", "freesound_community-bump-7-92964.mp3");
            _knock = LoadAudio("knock", "rohhsadotcom-knock-on-wood-02-421991.mp3");
        }
        else
        {
            Engine.Logger.LogWarning(
                "Spot Avalonia audio is disabled because no compatible audio backend is configured.");
        }

        _font = Engine.Managers.Fonts.LoadFromFile(
            "main",
            GetAssetPath("ArchitectsDaughter-Regular.ttf"));
    }

    protected override void LoadTilesheets()
    {
        var splash = Engine.Managers.Tilesheets.LoadFromImageFile(
            "splash",
            GetAssetPath("spot.png"));
        splash.ApplyMask(Color.Black.ToSKColor());

        _spotSheetDefault = Engine.Managers.Tilesheets.LoadFromImageFile(
            "spots",
            GetAssetPath("spot_defaults.png"));
        _spotSheetDefault.DefaultRegion.TileSize = new Size(93, 96);

        _spotSheetSelected = Engine.Managers.Tilesheets.LoadFromImageFile(
            "selected",
            GetAssetPath("spot_selected.png"));
        _spotSheetSelected.DefaultRegion.TileSize = new Size(64, 64);

        _clouds = Engine.Managers.Tilesheets.LoadFromImageFile(
            "clouds",
            GetAssetPath("clouds.png"));
    }

    private AudioResource LoadAudio(string key, string fileName)
    {
        return Engine.Managers.AudioResources.LoadFromFile(key, GetAssetPath(fileName));
    }

    private static string GetAssetPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "assets", fileName);
    }
}
