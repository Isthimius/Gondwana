using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Gondwana.Avalonia.Rendering;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Dialogs;
using Gondwana.Widgets.Menus;
using SkiaSharp;

namespace Gondwana.Demos.Spot;

/// <summary>
/// Avalonia desktop window for the shared Spot showcase.
/// </summary>
internal sealed class GameWindow : Window
{
    private const int MenuBarHeight = SpotMenuFactory.Height;
    private const int GpuTargetFps = 0;
    private const int GpuMsaaSampleCount = 4;

    private readonly AvaloniaGpuRenderSurfaceControl _renderSurface;

    private SpotGameHost? _gameHost;
    private MenuBarWidget? _menuBar;
    private HowToPlayDialog? _howToPlayDialog;
    private AboutBox? _aboutBox;
    private SKImage? _aboutSpotLogo;
    private SKImage? _aboutGondwanaLogo;
    private SKTypeface? _aboutTypeface;

    internal GameWindow()
    {
        Title = "Spot!";
        Width = 769;
        Height = 769 + MenuBarHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _renderSurface = new AvaloniaGpuRenderSurfaceControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        Content = _renderSurface;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        try
        {
            _gameHost = new SpotGameHost(_renderSurface);

            _gameHost.Engine.InitializationComplete += () =>
            {
                _gameHost.Engine.Configuration.TargetFPS = GpuTargetFps;
                _gameHost.Engine.Configuration.VSync = false;
                _gameHost.Engine.Configuration.MsaaSampleCount = GpuMsaaSampleCount;
            };

            ShowStartupSplashAndInitialize();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to initialize Spot: {ex}");
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _howToPlayDialog?.Dispose();
        _howToPlayDialog = null;

        _aboutBox?.Dispose();
        _aboutBox = null;

        _aboutSpotLogo?.Dispose();
        _aboutSpotLogo = null;

        _aboutGondwanaLogo?.Dispose();
        _aboutGondwanaLogo = null;

        _aboutTypeface?.Dispose();
        _aboutTypeface = null;

        _menuBar?.Dispose();
        _menuBar = null;

        _gameHost?.Dispose();
        _gameHost = null;

        base.OnClosed(e);
    }

    private void ShowStartupSplashAndInitialize()
    {
        if (_gameHost is null)
            throw new InvalidOperationException("Game host was not created before initialization.");

        _gameHost.Initialize();

        var host = _renderSurface.Host;
        var splash = _gameHost.CreateSplash(host, () =>
        {
            _gameHost.BeginPostSplashStartup();
            ApplyLoadedSettings();
            CreateMenu();
        });

        if (splash is null)
        {
            _gameHost.BeginPostSplashStartup();
            ApplyLoadedSettings();
            CreateMenu();
        }
    }

    private void ApplyLoadedSettings()
    {
        bool music = ReadBoolSetting(SpotSettings.Music, defaultValue: true);
        bool soundEffects = ReadBoolSetting(SpotSettings.SoundEffects, defaultValue: true);
        bool jiggle = ReadBoolSetting(SpotSettings.Jiggle, defaultValue: true);
        bool clouds = ReadBoolSetting(SpotSettings.Clouds, defaultValue: true);

        _gameHost!.Engine.EngineDispatcher.Post(() =>
        {
            _gameHost.SetMusicEnabled(music);
            _gameHost.SetSoundEffectsEnabled(soundEffects);
            _gameHost.SetJiggleEnabled(jiggle);
            _gameHost.SetCloudsEnabled(clouds);
        });
    }

    private bool ReadBoolSetting(string key, bool defaultValue)
    {
        if (_gameHost is null || !_gameHost.Engine.IsInitialized)
            return defaultValue;

        return SpotSettings.ReadBool(_gameHost.Engine.Configuration, key, defaultValue);
    }

    private void PersistSetting(string key, bool value)
    {
        if (_gameHost is null || !_gameHost.Engine.IsInitialized)
            return;

        SpotSettings.WriteBool(_gameHost.Engine, key, value);
    }

    private void CreateMenu()
    {
        if (_menuBar is not null || _gameHost is null)
            return;

        var view = _renderSurface.Host.ViewManager.Views[0];
        var state = new SpotMenuState(
            ReadBoolSetting(SpotSettings.Music, defaultValue: true),
            ReadBoolSetting(SpotSettings.SoundEffects, defaultValue: true),
            ReadBoolSetting(SpotSettings.Jiggle, defaultValue: true),
            ReadBoolSetting(SpotSettings.Clouds, defaultValue: true));

        _menuBar = SpotMenuFactory.Create(
            _renderSurface.Host,
            view,
            state,
            new SpotMenuActions
            {
                NewGame = () => _gameHost.OpenNewGameDialog(_gameHost.LastNewGameOptions),
                Exit = () => Dispatcher.UIThread.Post(Close),
                MusicChanged = SetMusicEnabled,
                SoundEffectsChanged = SetSoundEffectsEnabled,
                JiggleChanged = SetJiggleEnabled,
                CloudsChanged = SetCloudsEnabled,
                HowToPlay = OpenHowToPlayDialog,
                About = OpenAboutBox
            });
    }

    private void SetMusicEnabled(bool enabled)
    {
        PersistSetting(SpotSettings.Music, enabled);
        _gameHost?.Engine.EngineDispatcher.Post(() => _gameHost.SetMusicEnabled(enabled));
    }

    private void SetSoundEffectsEnabled(bool enabled)
    {
        PersistSetting(SpotSettings.SoundEffects, enabled);
        _gameHost?.Engine.EngineDispatcher.Post(() => _gameHost.SetSoundEffectsEnabled(enabled));
    }

    private void SetJiggleEnabled(bool enabled)
    {
        PersistSetting(SpotSettings.Jiggle, enabled);
        _gameHost?.Engine.EngineDispatcher.Post(() => _gameHost.SetJiggleEnabled(enabled));
    }

    private void SetCloudsEnabled(bool enabled)
    {
        PersistSetting(SpotSettings.Clouds, enabled);
        _gameHost?.Engine.EngineDispatcher.Post(() => _gameHost.SetCloudsEnabled(enabled));
    }

    private void OpenHowToPlayDialog()
    {
        if (_howToPlayDialog is not null)
        {
            _howToPlayDialog.Activate();
            return;
        }

        var dialog = new HowToPlayDialog(
            _renderSurface.Host,
            _renderSurface.Host.ViewManager.Views[0]);

        _howToPlayDialog = dialog;
        dialog.Closed += _ => _howToPlayDialog = null;
        dialog.Show();
        dialog.Activate();
    }

    private void OpenAboutBox()
    {
        if (_aboutBox is not null)
        {
            _aboutBox.Activate();
            return;
        }

        EnsureAboutResources();

        var about = SpotAboutBoxFactory.Create(
            _renderSurface.Host,
            _renderSurface.Host.ViewManager.Views[0],
            _aboutSpotLogo!,
            _aboutGondwanaLogo!,
            _aboutTypeface!,
            new DesktopExternalUriLauncher());

        _aboutBox = about;
        about.Closed += _ => _aboutBox = null;
        about.Show();
        about.Activate();
    }

    private void EnsureAboutResources()
    {
        string assetsPath = Path.Combine(AppContext.BaseDirectory, "assets");

        _aboutSpotLogo ??= LoadImage(Path.Combine(assetsPath, "spot.png"));
        _aboutGondwanaLogo ??= LoadImage(Path.Combine(assetsPath, "gondwana-logo-text.png"));
        _aboutTypeface ??= SKTypeface.FromFile(Path.Combine(assetsPath, "ArchitectsDaughter-Regular.ttf"))
            ?? throw new InvalidOperationException("Failed to load the Spot About-box font.");
    }

    private static SKImage LoadImage(string path)
    {
        return SKImage.FromEncodedData(path)
            ?? throw new InvalidOperationException($"Failed to decode About-box image: {path}");
    }

    private sealed class DesktopExternalUriLauncher : IExternalUriLauncher
    {
        public ValueTask OpenAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });

            return ValueTask.CompletedTask;
        }
    }
}
