using Gondwana.Assets;
using Gondwana.Audio;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Rendering;
using Gondwana.SkiaSharp;
using Gondwana.Widgets.Overlays;
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

    internal SplashScreen? CreateSplash(RenderSurfaceHostBase host, Action onSplashCompleted)
    {
        using var imageStream = RequireAsset(AssetTypes.Image, "gondwana-logo-text.png");
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

        using var fontStream = RequireAsset(AssetTypes.Font, "ArchitectsDaughter-Regular.ttf");
        _font = SKTypeface.FromStream(fontStream)
            ?? throw new InvalidOperationException("Failed to decode Spot font from the asset package.");

        _runtime.SetAudioResources(
            _music,
            _spotSelected,
            _spotDeselected,
            _velcro,
            _drop,
            _gameWin,
            _gameLose,
            _bump,
            _knock,
            _font);
    }

    protected override void LoadTilesheets()
    {
        using (var stream = RequireAsset(AssetTypes.Image, "spot.png"))
        {
            var splash = Engine.Managers.Tilesheets.LoadFromStream("splash", stream);
            splash.ApplyMask(Color.Black.ToSKColor());
        }

        _spotSheetDefault = Engine.Managers.Tilesheets.LoadFromDefinitionAsset(_assets, "spot_defaults.gts");
        _spotSheetSelected = Engine.Managers.Tilesheets.LoadFromDefinitionAsset(_assets, "spot_selected.gts");

        using var cloudStream = RequireAsset(AssetTypes.Image, "clouds.png");
        _clouds = Engine.Managers.Tilesheets.LoadFromStream("clouds", cloudStream);

        _runtime.SetTilesheets(
            _spotSheetDefault,
            _spotSheetSelected,
            _clouds);
    }

    internal Stream OpenAsset(AssetTypes type, string name) => RequireAsset(type, name);

    private AudioResource LoadAudio(string key, string assetName)
    {
        using var stream = RequireAsset(AssetTypes.Audio, assetName);
        return Engine.Managers.AudioResources.LoadFromStream(
            key,
            stream,
            Path.GetExtension(assetName));
    }

    private Stream RequireAsset(AssetTypes type, string name)
    {
        return _assets.Get(type, name)
            ?? throw new InvalidOperationException($"Spot asset '{type}:{name}' was not found in the package.");
    }
}
