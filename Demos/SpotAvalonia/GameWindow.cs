using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Gondwana.Configuration;
using Gondwana.Avalonia.Rendering;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.SpotAvalonia;

/// <summary>
/// The main game window for SpotAvalonia on desktop targets.
/// </summary>
internal sealed class GameWindow : Window
{
    private readonly AvaloniaBitmapRenderSurfaceControl _renderSurface = new();
    private SpotAvaloniaGameHost? _host;
    private NewGameOptions? _lastNewGameOptions;
    private EngineConfigurationFile? _configFile;
    private int _newGameDialogOpen;

    private const string ConfigSection   = "spotavalonia";
    private const string KeyMusic        = "music";
    private const string KeySoundEffects = "soundEffects";
    private const string KeyJiggle       = "jiggle";
    private const string KeyClouds       = "clouds";

    private MenuItem? _newGameMenuItem;
    private MenuItem? _musicMenuItem;
    private MenuItem? _soundEffectsMenuItem;
    private MenuItem? _jiggleMenuItem;
    private MenuItem? _cloudsMenuItem;

    internal GameWindow()
    {
        _configFile = EngineConfigurationFile.Load();

        Title     = "Spot (Avalonia)";
        Width     = 769;
        Height    = 800;   // render area (769) + menu bar
        CanResize = false;

        _renderSurface.HorizontalAlignment = HorizontalAlignment.Stretch;
        _renderSurface.VerticalAlignment   = VerticalAlignment.Stretch;

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

        var menu = BuildMenu();
        Grid.SetRow(menu, 0);
        grid.Children.Add(menu);

        Grid.SetRow(_renderSurface, 1);
        grid.Children.Add(_renderSurface);

        Content = grid;
    }

    private Menu BuildMenu()
    {
        var menu = new Menu();

        // ── Game menu ───────────────────────────────────────────────────────
        var gameMenu = new MenuItem { Header = "_Game" };

        _newGameMenuItem = new MenuItem { Header = "_New Game", IsEnabled = false };
        _newGameMenuItem.Click += async (_, _) => await OpenNewGameDialogAsync();

        var exitMenuItem = new MenuItem { Header = "E_xit" };
        exitMenuItem.Click += (_, _) => Close();

        gameMenu.Items.Add(_newGameMenuItem);
        gameMenu.Items.Add(new Separator());
        gameMenu.Items.Add(exitMenuItem);

        // ── Options menu ────────────────────────────────────────────────────
        var optionsMenu = new MenuItem { Header = "_Options" };

        _musicMenuItem = new MenuItem
        {
            Header     = "Music",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked  = ReadBoolSetting(KeyMusic, defaultValue: true),
        };
        _musicMenuItem.Click += (_, _) =>
        {
            var isChecked = _musicMenuItem.IsChecked;
            PersistSetting(KeyMusic, isChecked ? "true" : "false");
            _host?.Engine.EngineDispatcher.Post(() => _host.SetMusicEnabled(isChecked));
        };

        _soundEffectsMenuItem = new MenuItem
        {
            Header     = "Sound Effects",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked  = ReadBoolSetting(KeySoundEffects, defaultValue: true),
        };
        _soundEffectsMenuItem.Click += (_, _) =>
        {
            var isChecked = _soundEffectsMenuItem.IsChecked;
            PersistSetting(KeySoundEffects, isChecked ? "true" : "false");
            _host?.Engine.EngineDispatcher.Post(() => _host.SetSoundEffectsEnabled(isChecked));
        };

        _jiggleMenuItem = new MenuItem
        {
            Header     = "Jiggle",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked  = ReadBoolSetting(KeyJiggle, defaultValue: true),
        };
        _jiggleMenuItem.Click += (_, _) =>
        {
            var isChecked = _jiggleMenuItem.IsChecked;
            PersistSetting(KeyJiggle, isChecked ? "true" : "false");
            _host?.Engine.EngineDispatcher.Post(() => _host.SetJiggleEnabled(isChecked));
        };

        _cloudsMenuItem = new MenuItem
        {
            Header     = "Clouds",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked  = ReadBoolSetting(KeyClouds, defaultValue: true),
        };
        _cloudsMenuItem.Click += (_, _) =>
        {
            var isChecked = _cloudsMenuItem.IsChecked;
            PersistSetting(KeyClouds, isChecked ? "true" : "false");
            _host?.Engine.EngineDispatcher.Post(() => _host.SetCloudsEnabled(isChecked));
        };

        optionsMenu.Items.Add(_musicMenuItem);
        optionsMenu.Items.Add(_soundEffectsMenuItem);
        optionsMenu.Items.Add(_jiggleMenuItem);
        optionsMenu.Items.Add(_cloudsMenuItem);

        // ── Help menu ────────────────────────────────────────────────────────
        var helpMenu   = new MenuItem { Header = "_Help" };
        var aboutMenuItem = new MenuItem { Header = "_About" };
        aboutMenuItem.Click += async (_, _) => await OpenAboutDialogAsync();
        helpMenu.Items.Add(aboutMenuItem);

        menu.Items.Add(gameMenu);
        menu.Items.Add(optionsMenu);
        menu.Items.Add(helpMenu);

        return menu;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _host = new SpotAvaloniaGameHost(_renderSurface);
        _host.RequestNewGameDialog = () => Dispatcher.UIThread.Post(RequestNewGameDialog);

        // Subscribe before InitializeAsync() so the handler fires during initialization.
        _host.Engine.InitializationComplete += () =>
        {
            _host.Engine.Configuration.TargetFPS = 0;

            // Enable "New Game" now that the engine is ready.
            Dispatcher.UIThread.Post(() =>
            {
                if (_newGameMenuItem is not null)
                    _newGameMenuItem.IsEnabled = true;
            });
        };

        IsEnabled = false;
        try
        {
            await _host.InitializeAsync(logLevel: LogLevel.Warning);
            _host.BeginPostSplashStartup();
            ApplyLoadedSettings();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to initialize Spot: {ex.Message}");
            Close();
        }
        finally
        {
            IsEnabled = true;
            Activate();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _host?.Dispose();
        _host = null;
        base.OnClosed(e);
    }

    private void ApplyLoadedSettings()
    {
        bool music        = ReadBoolSetting(KeyMusic,        defaultValue: true);
        bool soundEffects = ReadBoolSetting(KeySoundEffects, defaultValue: true);
        bool jiggle       = ReadBoolSetting(KeyJiggle,       defaultValue: true);
        bool clouds       = ReadBoolSetting(KeyClouds,       defaultValue: true);

        // Sync menu checkboxes to persisted values.
        Dispatcher.UIThread.Post(() =>
        {
            if (_musicMenuItem        is not null) _musicMenuItem.IsChecked        = music;
            if (_soundEffectsMenuItem is not null) _soundEffectsMenuItem.IsChecked = soundEffects;
            if (_jiggleMenuItem       is not null) _jiggleMenuItem.IsChecked       = jiggle;
            if (_cloudsMenuItem       is not null) _cloudsMenuItem.IsChecked       = clouds;
        });

        _host!.Engine.EngineDispatcher.Post(() =>
        {
            _host.SetMusicEnabled(music);
            _host.SetSoundEffectsEnabled(soundEffects);
            _host.SetJiggleEnabled(jiggle);
            _host.SetCloudsEnabled(clouds);
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

        if (_host != null && _host.Engine.IsInitialized)
            _host.Engine.Configuration.SetConfigurationValue(ConfigSection, key, value);
    }

    private async Task OpenNewGameDialogAsync()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _newGameDialogOpen, 1, 0) != 0)
            return;

        try
        {
            var dialog = new NewGameDialog(_lastNewGameOptions);
            var options = await dialog.ShowDialog<NewGameOptions?>(this);
            _lastNewGameOptions = dialog.GetCurrentOptions();
            if (options is not null)
            {
                _host?.Engine.EngineDispatcher.Post(() => _host.StartNewGame(options));
            }
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _newGameDialogOpen, 0);
        }
    }

    private void RequestNewGameDialog()
    {
        _ = OpenNewGameDialogSafeAsync();
    }

    private async Task OpenNewGameDialogSafeAsync()
    {
        try
        {
            await OpenNewGameDialogAsync();
        }
        catch (Exception ex)
        {
            Engine.Logger.LogError(ex, "Failed to open New Game dialog.");
            await ShowErrorAsync($"Failed to open New Game dialog: {ex.Message}");
        }
    }

    private async Task OpenAboutDialogAsync()
    {
        var dialog = new AboutDialog();
        await dialog.ShowDialog(this);
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new Window
        {
            Title        = "Startup Error",
            Width        = 400,
            Height       = 160,
            CanResize    = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var stack = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        stack.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });

        var okButton = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Center };
        okButton.Click += (_, _) => dialog.Close();
        stack.Children.Add(okButton);

        dialog.Content = stack;
        await dialog.ShowDialog(this);
    }
}
