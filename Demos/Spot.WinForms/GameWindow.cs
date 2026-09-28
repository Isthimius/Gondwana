using Gondwana.Assets;
using System.Diagnostics;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Dialogs;
using Gondwana.Widgets.Menus;
using Gondwana.WinForms.Rendering;
using SkiaSharp;

namespace Gondwana.Demos.Spot;

internal partial class GameWindow : Form
{
    private SpotGameHost? _gameHost;
    private WinFormGpuRenderSurfaceControl? _gpuRenderSurface;
    private MenuBarWidget? _menuBar;
    private HowToPlayDialog? _howToPlayDialog;
    private AboutBox? _aboutBox;
    private SKImage? _aboutSpotLogo;
    private SKImage? _aboutGondwanaLogo;
    private SKTypeface? _aboutTypeface;

    private const int MenuBarHeight = SpotMenuFactory.Height;
    private static readonly Size DefaultWindowSize = new(769, 769 + MenuBarHeight);

    private const int GpuTargetFps = 0;
    private const int GpuMsaaSampleCount = 4;

    internal GameWindow()
    {
        InitializeComponent();

        CreateRenderSurface();

        // Normal window, centered
        FormBorderStyle = FormBorderStyle.FixedSingle;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = DefaultWindowSize;

        MinimizeBox = false;
        MaximizeBox = false;
    }

    private void CreateRenderSurface()
    {
        _gpuRenderSurface = new WinFormGpuRenderSurfaceControl
        {
            Dock = DockStyle.Fill
        };
        Controls.Add(_gpuRenderSurface);
    }

    // create the Game (and thereby start the engine) once the form & controls are ready
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        _gameHost = new SpotGameHost(_gpuRenderSurface!);

        // Subscribe before Initialize() is called so the handler fires during initialization.
        _gameHost.Engine.InitializationComplete += () =>
        {
            _gameHost.Engine.Configuration.TargetFPS = GpuTargetFps;
            _gameHost.Engine.Configuration.VSync = false;
            _gameHost.Engine.Configuration.MsaaSampleCount = GpuMsaaSampleCount;
        };
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        try
        {
            ShowStartupSplashAndInitialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Failed to initialize Spot: {ex.Message}",
                "Startup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Close();
        }
    }

    private void ShowStartupSplashAndInitialize()
    {
        if (_gameHost == null)
            throw new InvalidOperationException("Game host was not initialized before startup splash initialization.");

        Enabled = false;
        try
        {
            // Initializes the engine.
            _gameHost.Initialize();

            // Create and display the Gondwana splash screen.
            var host = _gpuRenderSurface!.Host;
            _gameHost.CreateSplash(host, () =>
            {
                // Create game visuals, start music, apply saved settings, and then expose
                // the in-engine menu once the splash has fully completed.
                _gameHost.BeginPostSplashStartup();
                ApplyLoadedSettings();
                CreateMenu();
            });
        }
        finally
        {
            Enabled = true;
            Activate();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Clean shutdown
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

        base.OnFormClosed(e);
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

    private void PersistSetting(string key, string value)
    {
        if (_gameHost is null || !_gameHost.Engine.IsInitialized)
            return;

        SpotSettings.WriteBool(
            _gameHost.Engine,
            key,
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
    }

    private void CreateMenu()
    {
        if (_menuBar != null || _gameHost == null || _gpuRenderSurface == null)
            return;

        var view = _gpuRenderSurface.Host.ViewManager.Views[0];
        var state = new SpotMenuState(
            ReadBoolSetting(SpotSettings.Music, defaultValue: true),
            ReadBoolSetting(SpotSettings.SoundEffects, defaultValue: true),
            ReadBoolSetting(SpotSettings.Jiggle, defaultValue: true),
            ReadBoolSetting(SpotSettings.Clouds, defaultValue: true));

        _menuBar = SpotMenuFactory.Create(
            _gpuRenderSurface.Host,
            view,
            state,
            new SpotMenuActions
            {
                NewGame = () => _gameHost.OpenNewGameDialog(_gameHost.LastNewGameOptions),
                Exit = () => BeginInvoke((Action)Close),
                MusicChanged = SetMusicEnabled,
                SoundEffectsChanged = SetSoundEffectsEnabled,
                JiggleChanged = SetJiggleEnabled,
                CloudsChanged = SetCloudsEnabled,
                HowToPlay = OpenHowToPlayDialog,
                About = OpenAboutBox
            });

        StartMonitoringMenuKeys();
    }

    private void StartMonitoringMenuKeys()
    {
        var keyboard = _gameHost?.Engine.Input.KeyboardEventPoller;
        if (keyboard == null)
            return;

        const double repeatIntervalSec = 0.10;

        for (int key = 'A'; key <= 'Z'; key++)
            keyboard.StartMonitoringKey(key, timeBetweenEvents: repeatIntervalSec);

        for (int key = '0'; key <= '9'; key++)
            keyboard.StartMonitoringKey(key, timeBetweenEvents: repeatIntervalSec);

        for (int key = 96; key <= 111; key++)
            keyboard.StartMonitoringKey(key, timeBetweenEvents: repeatIntervalSec);

        foreach (int key in new[]
                 {
                     8, 13, 27, 32, 33, 34, 35, 36, 37, 38, 39, 40, 46,
                     186, 187, 188, 189, 190, 191, 192, 219, 220, 221, 222
                 })
        {
            keyboard.StartMonitoringKey(key, timeBetweenEvents: repeatIntervalSec);
        }
    }

    private void SetMusicEnabled(bool enabled)
    {
        PersistSetting(SpotSettings.Music, enabled ? "true" : "false");
        _gameHost?.Engine.EngineDispatcher.Post(() => _gameHost.SetMusicEnabled(enabled));
    }

    private void SetSoundEffectsEnabled(bool enabled)
    {
        PersistSetting(SpotSettings.SoundEffects, enabled ? "true" : "false");
        _gameHost?.Engine.EngineDispatcher.Post(() => _gameHost.SetSoundEffectsEnabled(enabled));
    }

    private void SetJiggleEnabled(bool enabled)
    {
        PersistSetting(SpotSettings.Jiggle, enabled ? "true" : "false");
        _gameHost?.Engine.EngineDispatcher.Post(() => _gameHost.SetJiggleEnabled(enabled));
    }

    private void SetCloudsEnabled(bool enabled)
    {
        PersistSetting(SpotSettings.Clouds, enabled ? "true" : "false");
        _gameHost?.Engine.EngineDispatcher.Post(() => _gameHost.SetCloudsEnabled(enabled));
    }

    private void OpenHowToPlayDialog()
    {
        if (_howToPlayDialog is not null)
        {
            _howToPlayDialog.Activate();
            return;
        }

        if (_gpuRenderSurface is null)
            return;

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

        if (_gpuRenderSurface is null)
            return;

        EnsureAboutResources();

        var about = SpotAboutBoxFactory.Create(
            _gpuRenderSurface.Host,
            _gpuRenderSurface.Host.ViewManager.Views[0],
            _aboutSpotLogo!,
            _aboutGondwanaLogo!,
            _aboutTypeface!,
            new WinFormsExternalUriLauncher());

        _aboutBox = about;
        about.Closed += _ => _aboutBox = null;
        about.Show();
        about.Activate();
    }

    private void EnsureAboutResources()
    {
        using var fontStream = _gameHost!.OpenAsset(AssetTypes.Font, "ArchitectsDaughter-Regular.ttf");

        _aboutSpotLogo ??= LoadImage("spot.png");
        _aboutGondwanaLogo ??= LoadImage("gondwana-logo-text.png");
        _aboutTypeface ??= SKTypeface.FromStream(fontStream)
            ?? throw new InvalidOperationException("Failed to load the Spot About-box font.");
    }

    private SKImage LoadImage(string path)
    {
        using var stream = _gameHost!.OpenAsset(AssetTypes.Image, path);
        return SKImage.FromEncodedData(stream)
            ?? throw new InvalidOperationException($"Failed to decode About-box image: {path}");
    }

    private sealed class WinFormsExternalUriLauncher : IExternalUriLauncher
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
