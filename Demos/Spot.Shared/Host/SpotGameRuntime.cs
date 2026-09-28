using Gondwana.Audio;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Gondwana.Widgets;
using SkiaSharp;

namespace Gondwana.Demos.Spot;

internal sealed partial class SpotGameRuntime
{
    private const int PersistentMenuHeight = 32;

    private readonly int _scoreToggleKey;
    private readonly RenderSurfaceHostBase _surfaceHost;
    private readonly Func<WidgetInputRouter?> _widgetInputRouterAccessor;
    private readonly Action<KeyboardEventPoller>? _configurePlatformKeyboardInput;
    private readonly Action<NewGameDialog>? _configureNewGameDialogForPlatform;
    private readonly Action? _persistGameState;

    private Scene? _scene;

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

    private static Gondwana.Engine Engine => Gondwana.Engine.Instance;

    private Scene? Scene => _scene;

    private Scene ActiveScene => _scene
        ?? throw new InvalidOperationException("Spot scene has not been created.");

    private RenderSurfaceHostBase SurfaceHost => _surfaceHost;
    private int SurfaceWidth => _surfaceHost.Backbuffer.Width;
    private int SurfaceHeight => _surfaceHost.Backbuffer.Height;
    private WidgetInputRouter? WidgetInputRouter => _widgetInputRouterAccessor();
    private int ScoreToggleKey => _scoreToggleKey;

    internal SpotGameRuntime(
        RenderSurfaceHostBase surfaceHost,
        int scoreToggleKey,
        Func<WidgetInputRouter?> widgetInputRouterAccessor,
        Action<KeyboardEventPoller>? configurePlatformKeyboardInput = null,
        Action<NewGameDialog>? configureNewGameDialogForPlatform = null,
        Action? persistGameState = null)
    {
        _surfaceHost = surfaceHost ?? throw new ArgumentNullException(nameof(surfaceHost));
        _scoreToggleKey = scoreToggleKey;
        _widgetInputRouterAccessor = widgetInputRouterAccessor
            ?? throw new ArgumentNullException(nameof(widgetInputRouterAccessor));
        _configurePlatformKeyboardInput = configurePlatformKeyboardInput;
        _configureNewGameDialogForPlatform = configureNewGameDialogForPlatform;
        _persistGameState = persistGameState;
    }

    internal bool AudioAvailable => _music is not null;

    internal void SetAudioResources(
        AudioResource? music,
        AudioResource? spotSelected,
        AudioResource? spotDeselected,
        AudioResource? velcro,
        AudioResource? drop,
        AudioResource? gameWin,
        AudioResource? gameLose,
        AudioResource? bump,
        AudioResource? knock,
        SKTypeface font)
    {
        _music = music;
        _spotSelected = spotSelected;
        _spotDeselected = spotDeselected;
        _velcro = velcro;
        _drop = drop;
        _gameWin = gameWin;
        _gameLose = gameLose;
        _bump = bump;
        _knock = knock;
        _font = font;
    }

    internal void SetTilesheets(
        Tilesheet spotSheetDefault,
        Tilesheet spotSheetSelected,
        Tilesheet clouds)
    {
        _spotSheetDefault = spotSheetDefault;
        _spotSheetSelected = spotSheetSelected;
        _clouds = clouds;
    }
}
