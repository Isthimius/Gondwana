using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Gondwana.Configuration;
using Gondwana.Demos.Spot.Hosts;
using Gondwana.Drawing.Direct;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Dialogs;
using Gondwana.Widgets.Menus;
using Gondwana.WinForms.Rendering;
using SkiaSharp;

namespace Gondwana.Demos.Spot;

internal partial class GameWindow : Form
{
    private ISpotGameHost? _gameHost;
    private WinFormGpuRenderSurfaceControl? _gpuRenderSurface;
    private EngineConfigurationFile? _configFile;
    private MenuBarWidget? _menuBar;
    private AboutBox? _aboutBox;
    private SKImage? _aboutSpotLogo;
    private SKImage? _aboutGondwanaLogo;
    private SKTypeface? _aboutTypeface;

    private static readonly Size DefaultWindowSize = new(769, 769);

    private const string ConfigSection = "spot";
    private const string KeyMusic = "music";
    private const string KeySoundEffects = "soundEffects";
    private const string KeyJiggle = "jiggle";
    private const string KeyClouds = "clouds";
    private const string RepoUrl = "https://github.com/isthimius/gondwana";
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
                    OpenAboutBox,
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

        var host = _gpuRenderSurface.Host;
        var view = host.ViewManager.Views[0];
        Rectangle viewport = view.Viewport.TargetRectPx;

        // The former WinForms dialog used a 420x570 client area plus its native title bar.
        // Keeping 420x606 here preserves nearly the same overall proportions while allowing
        // the Widgets title bar to live inside the dialog bounds.
        const int dialogWidth = 420;
        const int dialogHeight = 606;
        var bounds = new Rectangle(
            viewport.Left + (viewport.Width - dialogWidth) / 2,
            viewport.Top + (viewport.Height - dialogHeight) / 2,
            dialogWidth,
            dialogHeight);

        var about = new AboutBox(
            host,
            view,
            applicationName: "Spot!",
            version: string.Empty,
            description: "Built with Gondwana Game Engine",
            logo: _aboutSpotLogo,
            bounds: bounds,
            nickname: "spot.about",
            uriLauncher: new WinFormsExternalUriLauncher(),
            hyperlinkUri: new Uri(RepoUrl),
            hyperlinkText: "View Gondwana on GitHub");

        _aboutBox = about;
        about.Closed += _ => _aboutBox = null;

        // Match the previous About window's black client area and square-edged,
        // understated presentation rather than the stock generic dialog styling.
        about.Panel.SetColor(Color.Black)
                   .SetBorderColor(Color.FromArgb(255, 90, 90, 90))
                   .SetCornerRadius(0f);
        about.TitleBar.SetColor(Color.FromArgb(255, 32, 32, 32))
                      .SetCornerRadius(0f);
        about.TitleText.SetText("About Spot!")
                       .SetColors(SKColors.White, SKColors.Transparent);

        // Reuse the built-in logo slot for the large Spot artwork and size it to
        // the same 360x240 region as the old PictureBox.
        if (about.Logo is not null)
        {
            about.Logo.ScreenBounds = new Rectangle(bounds.Left + 30, bounds.Top + 36, 360, 240);
            about.SetLocalOffset(about.Logo, new Vector2(30, 36));
            about.Logo.SetScaleMode(DirectImage.ScaleMode.Fit);
        }

        // The stock AboutBox supports one logo. Add the former Gondwana logo as
        // another owned DirectImage so the visual hierarchy remains unchanged.
        var gondwanaLogo = new DirectImage(
            _aboutGondwanaLogo!,
            host,
            view,
            new Rectangle(bounds.Left + 110, bounds.Top + 231, 200, 200),
            "spot.about.gondwanaLogo")
            .SetScaleMode(DirectImage.ScaleMode.Fit);
        gondwanaLogo.ZOrder = 10_002;
        about.Add(gondwanaLogo);

        // Repurpose the AboutBox header text for the same custom-font caption the
        // WinForms dialog displayed near the bottom.
        about.HeaderText.ScreenBounds = new Rectangle(bounds.Left, bounds.Top + 441, 420, 30);
        about.SetLocalOffset(about.HeaderText, new Vector2(0, 441));
        about.HeaderText.SetText("Built with Gondwana Game Engine")
                        .SetFont(_aboutTypeface!, 21f, minSize: 16f)
                        .SetColors(SKColors.White, SKColors.Transparent)
                        .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                        .EnableWrapping(false);

        about.VersionText.SetText(string.Empty);
        about.DetailsText.SetText(string.Empty);

        if (about.Hyperlink is not null)
        {
            about.Hyperlink.Label.ScreenBounds = new Rectangle(bounds.Left, bounds.Top + 486, 420, 25);
            about.SetLocalOffset(about.Hyperlink, new Vector2(0, 486));
            about.Hyperlink.Label.SetFont(SKTypeface.Default, 19f, minSize: 14f)
                                 .SetColors(new SKColor(135, 206, 250), SKColors.Transparent)
                                 .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                                 .EnableWrapping(false);
        }

        // Match the former 100x32 centered OK button.
        about.OkButton.Background.ScreenBounds = new Rectangle(bounds.Left + 160, bounds.Top + 541, 100, 32);
        about.OkButton.Label.ScreenBounds = new Rectangle(bounds.Left + 160, bounds.Top + 541, 100, 32);
        about.SetLocalOffset(about.OkButton, new Vector2(160, 541));
        about.OkButton.SetBackgroundColors(
                         Color.FromArgb(255, 52, 52, 52),
                         Color.FromArgb(255, 70, 70, 70),
                         Color.FromArgb(255, 38, 38, 38))
                      .SetTextColor(Color.White);

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
        using SKData data = SKData.Create(path)
            ?? throw new InvalidOperationException($"Failed to read About-box image: {path}");

        return SKImage.FromEncodedData(data)
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
