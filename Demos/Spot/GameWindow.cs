using System;
using System.Drawing;
using System.Windows.Forms;
using Gondwana.Configuration;
using Gondwana.Demos.Spot.Hosts;
using Gondwana.Widgets.Menus;
using Gondwana.WinForms.Rendering;

namespace Gondwana.Demos.Spot;

internal partial class GameWindow : Form
{
    private ISpotGameHost? _gameHost;
    private WinFormGpuRenderSurfaceControl? _gpuRenderSurface;
    private EngineConfigurationFile? _configFile;
    private MenuBarWidget? _menuBar;

    private static readonly Size DefaultWindowSize = new(769, 769);

    private const string ConfigSection = "spot";
    private const string KeyMusic = "music";
    private const string KeySoundEffects = "soundEffects";
    private const string KeyJiggle = "jiggle";
    private const string KeyClouds = "clouds";
    private const int GpuTargetFps = 0;
    private const int GpuMsaaSampleCount = 4;

    internal GameWindow()
    {
        InitializeComponent();

        // Avoid config file I/O at design time (the Designer instantiates the form without a
        // real runtime environment, so file access can fail or produce wrong defaults).
        if (!System.ComponentModel.LicenseManager.UsageMode.Equals(
                System.ComponentModel.LicenseUsageMode.Designtime))
        {
            _configFile = EngineConfigurationFile.Load();
        }

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

        _gameHost = new SpotGpuGameHost(_gpuRenderSurface!);

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
        _menuBar?.Dispose();
        _menuBar = null;

        _gameHost?.Dispose();
        _gameHost = null;

        base.OnFormClosed(e);
    }

    private void ApplyLoadedSettings()
    {
        bool music = ReadBoolSetting(KeyMusic, defaultValue: true);
        bool soundEffects = ReadBoolSetting(KeySoundEffects, defaultValue: true);
        bool jiggle = ReadBoolSetting(KeyJiggle, defaultValue: true);
        bool clouds = ReadBoolSetting(KeyClouds, defaultValue: true);

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
        if (_configFile == null)
            return defaultValue;

        var raw = _configFile.EngineConfig.GetConfigurationValue(
            ConfigSection,
            key,
            defaultValue ? "true" : "false");

        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
    }

    private void PersistSetting(string key, string value)
    {
        if (_configFile == null)
            return;

        _configFile.EngineConfig.SetConfigurationValue(ConfigSection, key, value);
        _configFile.Save();

        if (_gameHost != null && _gameHost.Engine.IsInitialized)
            _gameHost.Engine.Configuration.SetConfigurationValue(ConfigSection, key, value);
    }

    private void CreateMenu()
    {
        if (_menuBar != null || _gameHost == null || _gpuRenderSurface == null)
            return;

        var view = _gpuRenderSurface.Host.ViewManager.Views[0];
        _menuBar = new MenuBarWidget(
            _gpuRenderSurface.Host,
            view,
            new Rectangle(0, 0, view.Viewport.TargetRectPx.Width, 32));

        _menuBar
            .AddMenu(
                "Game",
                game => game
                    .AddItem(
                        "New Game",
                        () => _gameHost.OpenNewGameDialog(_gameHost.LastNewGameOptions),
                        mnemonic: 'N')
                    .AddSeparator()
                    .AddItem(
                        "Exit",
                        () => BeginInvoke((Action)Close),
                        mnemonic: 'X'),
                mnemonic: 'G')
            .AddMenu(
                "Options",
                options => options
                    .AddCheckItem(
                        "Music",
                        enabled => SetMusicEnabled(enabled),
                        isChecked: ReadBoolSetting(KeyMusic, defaultValue: true),
                        key: "options.music",
                        mnemonic: 'M')
                    .AddCheckItem(
                        "Sound Effects",
                        enabled => SetSoundEffectsEnabled(enabled),
                        isChecked: ReadBoolSetting(KeySoundEffects, defaultValue: true),
                        key: "options.soundEffects",
                        mnemonic: 'S')
                    .AddCheckItem(
                        "Jiggle",
                        enabled => SetJiggleEnabled(enabled),
                        isChecked: ReadBoolSetting(KeyJiggle, defaultValue: true),
                        key: "options.jiggle",
                        mnemonic: 'J')
                    .AddCheckItem(
                        "Clouds",
                        enabled => SetCloudsEnabled(enabled),
                        isChecked: ReadBoolSetting(KeyClouds, defaultValue: true),
                        key: "options.clouds",
                        mnemonic: 'C'),
                mnemonic: 'O')
            .AddMenu(
                "Help",
                help => help.AddItem(
                    "About",
                    () => BeginInvoke((Action)OpenAboutDialog),
                    mnemonic: 'A'),
                mnemonic: 'H');

        _menuBar.Show();
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

        foreach (int key in new[] { 13, 27, 32, 37, 38, 39, 40 })
            keyboard.StartMonitoringKey(key, timeBetweenEvents: repeatIntervalSec);
    }

    private void SetMusicEnabled(bool enabled)
    {
        PersistSetting(KeyMusic, enabled ? "true" : "false");
        _gameHost?.Engine.EngineDispatcher.Post(() => _gameHost.SetMusicEnabled(enabled));
    }

    private void SetSoundEffectsEnabled(bool enabled)
    {
        PersistSetting(KeySoundEffects, enabled ? "true" : "false");
        _gameHost?.Engine.EngineDispatcher.Post(() => _gameHost.SetSoundEffectsEnabled(enabled));
    }

    private void SetJiggleEnabled(bool enabled)
    {
        PersistSetting(KeyJiggle, enabled ? "true" : "false");
        _gameHost?.Engine.EngineDispatcher.Post(() => _gameHost.SetJiggleEnabled(enabled));
    }

    private void SetCloudsEnabled(bool enabled)
    {
        PersistSetting(KeyClouds, enabled ? "true" : "false");
        _gameHost?.Engine.EngineDispatcher.Post(() => _gameHost.SetCloudsEnabled(enabled));
    }

    private void OpenAboutDialog()
    {
        using var dialog = new AboutDialog();
        dialog.ShowDialog(this);
    }
}
