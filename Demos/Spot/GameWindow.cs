using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Gondwana.Configuration;
using Gondwana.WinForms.Rendering;
using Gondwana.Demos.Spot.Hosts;

namespace Gondwana.Demos.Spot;

internal partial class GameWindow : Form
{
    private ISpotGameHost? _gameHost;
    private WinFormBitmapRenderSurfaceControl? _bitmapRenderSurface;
    private WinFormGpuRenderSurfaceControl? _gpuRenderSurface;
    private EngineConfigurationFile? _configFile;
    private static readonly Size DefaultWindowSize = new(769, 769);
    private MenuStrip _menuStrip = null!;

    private const string ConfigSection = "spot";
    private const string KeyMusic = "music";
    private const string KeySoundEffects = "soundEffects";
    private const string KeyJiggle = "jiggle";
    private const string KeyClouds = "clouds";
    private const string KeyGpuAcceleration = "gpuAcceleration";
    private const int GpuTargetFps = 500;
    private const int GpuMsaaSampleCount = 4;

    private bool _gpuAcceleration;

    internal GameWindow()
    {
        InitializeComponent();

        // Avoid config file I/O at design time (the Designer instantiates the form without a
        // real runtime environment, so file access can fail or produce wrong defaults).
        if (!System.ComponentModel.LicenseManager.UsageMode.Equals(
                System.ComponentModel.LicenseUsageMode.Designtime))
        {
            _configFile = EngineConfigurationFile.Load();
            _gpuAcceleration = ReadBoolSetting(KeyGpuAcceleration, defaultValue: false);
        }

        CreateRenderSurface();
        CreateMenu();

        // Normal window, centered
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.ClientSize = DefaultWindowSize;

        this.MinimizeBox = false;
        this.MaximizeBox = false;
    }

    private void CreateRenderSurface()
    {
        if (_gpuAcceleration)
        {
            _gpuRenderSurface = new WinFormGpuRenderSurfaceControl();
            _gpuRenderSurface.Dock = DockStyle.Fill;
            Controls.Add(_gpuRenderSurface);
        }
        else
        {
            _bitmapRenderSurface = new WinFormBitmapRenderSurfaceControl();
            _bitmapRenderSurface.Dock = DockStyle.Fill;
            Controls.Add(_bitmapRenderSurface);
        }
    }

    // create the Game (and thereby start the engine) once the form & controls are ready
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (_gpuAcceleration)
            _gameHost = new SpotGpuGameHost(_gpuRenderSurface!);
        else
            _gameHost = new SpotGameHost(_bitmapRenderSurface!);

        // Subscribe before Initialize() is called so the handler fires during initialization.
        _gameHost.Engine.InitializationComplete += () =>
        {
            if (_gpuAcceleration)
            {
                _gameHost.Engine.Configuration.TargetFPS = GpuTargetFps;
                _gameHost.Engine.Configuration.VSync = false;
                _gameHost.Engine.Configuration.MsaaSampleCount = GpuMsaaSampleCount;
            }
            else
            {
                _gameHost.Engine.Configuration.TargetFPS = 0;
            }
        };
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // resize client area to include the menu strip
        this.ClientSize = new Size(DefaultWindowSize.Width, DefaultWindowSize.Height + _menuStrip.Height);
        try
        {
            await ShowStartupSplashAndInitializeAsync();
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

    private async Task ShowStartupSplashAndInitializeAsync()
    {
        if (_gameHost == null)
            throw new InvalidOperationException("Game host was not initialized before startup splash initialization.");

        Enabled = false;
        try
        {
            await _gameHost.InitializeAsync();
            _gameHost.BeginPostSplashStartup();

            // Apply saved settings now that assets are loaded and engine is running.
            ApplyLoadedSettings();
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
        var raw = _configFile.EngineConfig.GetConfigurationValue(ConfigSection, key, defaultValue ? "true" : "false");
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

    private ToolStripMenuItem? _musicMenuItem;
    private ToolStripMenuItem? _soundEffectsMenuItem;
    private ToolStripMenuItem? _jiggleMenuItem;
    private ToolStripMenuItem? _cloudsMenuItem;
    private ToolStripMenuItem? _gpuAccelerationMenuItem;

    private void CreateMenu()
    {
        _menuStrip = new MenuStrip();

        #region Game menu
        var gameMenu = new ToolStripMenuItem("Game");
var newGameMenuItem = new ToolStripMenuItem("New Game", null, (s, e) => _gameHost?.OpenNewGameDialog(_gameHost?.LastNewGameOptions));
        var exitMenuItem = new ToolStripMenuItem("Exit", null, (s, e) => Close());

        gameMenu.DropDownItems.Add(newGameMenuItem);
        gameMenu.DropDownItems.Add(new ToolStripSeparator());
        gameMenu.DropDownItems.Add(exitMenuItem);
        #endregion Game menu

        #region Options menu
        var optionsMenu = new ToolStripMenuItem("Options");

        _musicMenuItem = new ToolStripMenuItem("Music")
        {
            CheckOnClick = true,
            Checked = ReadBoolSetting(KeyMusic, defaultValue: true)
        };
        _musicMenuItem.CheckedChanged += MusicMenuItem_CheckedChanged;

        _soundEffectsMenuItem = new ToolStripMenuItem("Sound Effects")
        {
            CheckOnClick = true,
            Checked = ReadBoolSetting(KeySoundEffects, defaultValue: true)
        };
        _soundEffectsMenuItem.CheckedChanged += SoundEffectsMenuItem_CheckedChanged;

        _jiggleMenuItem = new ToolStripMenuItem("Jiggle")
        {
            CheckOnClick = true,
            Checked = ReadBoolSetting(KeyJiggle, defaultValue: true)
        };
        _jiggleMenuItem.CheckedChanged += JiggleMenuItem_CheckedChanged;

        _cloudsMenuItem = new ToolStripMenuItem("Clouds")
        {
            CheckOnClick = true,
            Checked = ReadBoolSetting(KeyClouds, defaultValue: true)
        };
        _cloudsMenuItem.CheckedChanged += CloudsMenuItem_CheckedChanged;

        _gpuAccelerationMenuItem = new ToolStripMenuItem("GPU Acceleration")
        {
            CheckOnClick = true,
            Checked = _gpuAcceleration
        };
        _gpuAccelerationMenuItem.CheckedChanged += GpuAccelerationMenuItem_CheckedChanged;

        optionsMenu.DropDownItems.Add(_musicMenuItem);
        optionsMenu.DropDownItems.Add(_soundEffectsMenuItem);
        optionsMenu.DropDownItems.Add(_jiggleMenuItem);
        optionsMenu.DropDownItems.Add(_cloudsMenuItem);
        optionsMenu.DropDownItems.Add(new ToolStripSeparator());
        optionsMenu.DropDownItems.Add(_gpuAccelerationMenuItem);
        #endregion Options menu

        #region Help menu
        var helpMenu = new ToolStripMenuItem("Help");
        var aboutMenuItem = new ToolStripMenuItem("About", null, (s, e) => OpenAboutDialog());
        helpMenu.DropDownItems.Add(aboutMenuItem);
        #endregion Help menu

        _menuStrip.Items.Add(gameMenu);
        _menuStrip.Items.Add(optionsMenu);
        _menuStrip.Items.Add(helpMenu);

        MainMenuStrip = _menuStrip;
        Controls.Add(_menuStrip);
    }

    private void MusicMenuItem_CheckedChanged(object? sender, EventArgs e)
    {
        var enabled = _musicMenuItem!.Checked;
        PersistSetting(KeyMusic, enabled ? "true" : "false");
        if (_gameHost != null)
            _gameHost.Engine.EngineDispatcher.Post(() => _gameHost.SetMusicEnabled(enabled));
    }

    private void SoundEffectsMenuItem_CheckedChanged(object? sender, EventArgs e)
    {
        var enabled = _soundEffectsMenuItem!.Checked;
        PersistSetting(KeySoundEffects, enabled ? "true" : "false");
        if (_gameHost != null)
            _gameHost.Engine.EngineDispatcher.Post(() => _gameHost.SetSoundEffectsEnabled(enabled));
    }

    private void JiggleMenuItem_CheckedChanged(object? sender, EventArgs e)
    {
        var enabled = _jiggleMenuItem!.Checked;
        PersistSetting(KeyJiggle, enabled ? "true" : "false");
        if (_gameHost != null)
            _gameHost.Engine.EngineDispatcher.Post(() => _gameHost.SetJiggleEnabled(enabled));
    }

    private void CloudsMenuItem_CheckedChanged(object? sender, EventArgs e)
    {
        var enabled = _cloudsMenuItem!.Checked;
        PersistSetting(KeyClouds, enabled ? "true" : "false");
        if (_gameHost != null)
            _gameHost.Engine.EngineDispatcher.Post(() => _gameHost.SetCloudsEnabled(enabled));
    }

    private void GpuAccelerationMenuItem_CheckedChanged(object? sender, EventArgs e)
    {
        var enabled = _gpuAccelerationMenuItem!.Checked;
        PersistSetting(KeyGpuAcceleration, enabled ? "true" : "false");

        if (IsHandleCreated)
        {
            BeginInvoke((Action)ShowGpuAccelerationRestartRequiredMessage);
            return;
        }

        ShowGpuAccelerationRestartRequiredMessage();
    }

    private void ShowGpuAccelerationRestartRequiredMessage()
    {
        MessageBox.Show(
            this,
            "GPU Acceleration setting has been changed. Please restart the application to apply this change.",
            "Restart Required",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OpenAboutDialog()
    {
        using var dialog = new AboutDialog();
        dialog.ShowDialog(this);
    }
}
