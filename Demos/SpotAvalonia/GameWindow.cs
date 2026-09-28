using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
/// Thin Avalonia desktop shell for the shared Spot game.
/// </summary>
internal sealed class GameWindow : Window
{
    private readonly AvaloniaGpuRenderSurfaceControl _gpuRenderSurface = new();

    private SpotGameHost? _gameHost;
    private MenuBarWidget? _menuBar;
    private HowToPlayDialog? _howToPlayDialog;
    private AboutBox? _aboutBox;
    private SKImage? _aboutSpotLogo;
    private SKImage? _aboutGondwanaLogo;
    private SKTypeface? _aboutTypeface;

    private const int MenuBarHeight = SpotMenuFactory.Height;
    private const int DefaultWidth = 769;
    private const int DefaultHeight = 769 + MenuBarHeight;

    private const int GpuTargetFps = 0;
    private const int GpuMsaaSampleCount = 4;

    internal GameWindow()
    {
        Title = "Spot!";
        Width = DefaultWidth;
        Height = DefaultHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _gpuRenderSurface.HorizontalAlignment = HorizontalAlignment.Stretch;
        _gpuRenderSurface.VerticalAlignment = VerticalAlignment.Stretch;

        Content = _gpuRenderSurface;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        _gameHost = new SpotGameHost(_gpuRenderSurface);

        // Subscribe before Initialize() so the handler runs during initialization.
        _gameHost.Engine.InitializationComplete += () =>
        {
            _gameHost.Engine.Configuration.TargetFPS = GpuTargetFps;
            _gameHost.Engine.Configuration.VSync = false;
            _gameHost.Engine.Configuration.MsaaSampleCount = GpuMsaaSampleCount;
        };

        try
        {
            ShowStartupSplashAndInitialize();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Failed to initialize Spot Avalonia: {ex}");
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
            throw new InvalidOperationException("Game host was not initialized before startup.");

        IsEnabled = false;

        try
        {
            _gameHost.Initialize();

            void CompleteStartup()
            {
                _gameHost.BeginPostSplashStartup();
                ApplyLoadedSettings();
                CreateMenu();
            }

            var splash = _gameHost.CreateSplash(_gpuRenderSurface.Host, CompleteStartup);
            if (splash is null)
                CompleteStartup();
        }
        finally
        {
            IsEnabled = true;
            Activate();
        }
    }

    private void ApplyLoadedSettings()
    {
        if (_gameHost is null)
            return;

        bool audioAvailable = _gameHost.AudioAvailable;
        bool music = audioAvailable && ReadBoolSetting(SpotSettings.Music, defaultValue: true);
        bool soundEffects = audioAvailable && ReadBoolSetting(SpotSettings.SoundEffects, defaultValue: true);
        bool jiggle = ReadBoolSetting(SpotSettings.Jiggle, defaultValue: true);
        bool clouds = ReadBoolSetting(SpotSettings.Clouds, defaultValue: true);

        _gameHost.Engine.EngineDispatcher.Post(() =>
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

        bool audioAvailable = _gameHost.AudioAvailable;
        var view = _gpuRenderSurface.Host.ViewManager.Views[0];
        var state = new SpotMenuState(
            audioAvailable && ReadBoolSetting(SpotSettings.Music, defaultValue: true),
            audioAvailable && ReadBoolSetting(SpotSettings.SoundEffects, defaultValue: true),
            ReadBoolSetting(SpotSettings.Jiggle, defaultValue: true),
            ReadBoolSetting(SpotSettings.Clouds, defaultValue: true));

        _menuBar = SpotMenuFactory.Create(
            _gpuRenderSurface.Host,
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

        if (!audioAvailable)
        {
            _menuBar["options.music"].SetEnabled(false);
            _menuBar["options.soundEffects"].SetEnabled(false);
        }
    }

    private void SetMusicEnabled(bool enabled)
    {
        if (_gameHost?.AudioAvailable != true)
            return;

        PersistSetting(SpotSettings.Music, enabled);
        _gameHost.Engine.EngineDispatcher.Post(() => _gameHost.SetMusicEnabled(enabled));
    }

    private void SetSoundEffectsEnabled(bool enabled)
    {
        if (_gameHost?.AudioAvailable != true)
            return;

        PersistSetting(SpotSettings.SoundEffects, enabled);
        _gameHost.Engine.EngineDispatcher.Post(() => _gameHost.SetSoundEffectsEnabled(enabled));
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
            _gpuRenderSurface.Host,
            _gpuRenderSurface.Host.ViewManager.Views[0]);

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
            _gpuRenderSurface.Host,
            _gpuRenderSurface.Host.ViewManager.Views[0],
            _aboutSpotLogo!,
            _aboutGondwanaLogo!,
            _aboutTypeface!,
            new AvaloniaExternalUriLauncher());

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

    private sealed class AvaloniaExternalUriLauncher : IExternalUriLauncher
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
