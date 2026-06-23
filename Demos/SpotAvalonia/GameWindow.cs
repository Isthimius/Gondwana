using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
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

    private MenuItem? _newGameMenuItem;
    private MenuItem? _musicMenuItem;
    private MenuItem? _soundEffectsMenuItem;
    private MenuItem? _jiggleMenuItem;
    private MenuItem? _cloudsMenuItem;

    internal GameWindow()
    {
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
            IsChecked  = true,
        };
        _musicMenuItem.Click += (_, _) =>
        {
            var isChecked = _musicMenuItem.IsChecked;
            _host?.Engine.EngineDispatcher.Post(() => _host.SetMusicEnabled(isChecked));
        };

        _soundEffectsMenuItem = new MenuItem
        {
            Header     = "Sound Effects",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked  = true,
        };
        _soundEffectsMenuItem.Click += (_, _) =>
        {
            var isChecked = _soundEffectsMenuItem.IsChecked;
            _host?.Engine.EngineDispatcher.Post(() => _host.SetSoundEffectsEnabled(isChecked));
        };

        _jiggleMenuItem = new MenuItem
        {
            Header     = "Jiggle",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked  = true,
        };
        _jiggleMenuItem.Click += (_, _) =>
        {
            var isChecked = _jiggleMenuItem.IsChecked;
            _host?.Engine.EngineDispatcher.Post(() => _host.SetJiggleEnabled(isChecked));
        };

        _cloudsMenuItem = new MenuItem
        {
            Header     = "Clouds",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked  = true,
        };
        _cloudsMenuItem.Click += (_, _) =>
        {
            var isChecked = _cloudsMenuItem.IsChecked;
            _host?.Engine.EngineDispatcher.Post(() => _host.SetCloudsEnabled(isChecked));
        };

        optionsMenu.Items.Add(_musicMenuItem);
        optionsMenu.Items.Add(_soundEffectsMenuItem);
        optionsMenu.Items.Add(_jiggleMenuItem);
        optionsMenu.Items.Add(_cloudsMenuItem);

        // ── Help menu ───────────────────────────────────────────────────────
        var helpMenu = new MenuItem { Header = "_Help" };

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

        // Subscribe before Initialize() so the handler fires during initialization.
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

        _host.Initialize();
    }

    protected override void OnClosed(EventArgs e)
    {
        _host?.Dispose();
        _host = null;
        base.OnClosed(e);
    }

    private async System.Threading.Tasks.Task OpenNewGameDialogAsync()
    {
#if !BROWSER
        var dialog = new NewGameDialog(_lastNewGameOptions);
        var options = await dialog.ShowDialog<NewGameOptions?>(this);
        if (options is not null)
        {
            _lastNewGameOptions = options;
            _host?.Engine.EngineDispatcher.Post(() => _host.StartNewGame(options));
        }
#else
        await System.Threading.Tasks.Task.CompletedTask;
#endif
    }

    private async System.Threading.Tasks.Task OpenAboutDialogAsync()
    {
#if !BROWSER
        var dialog = new AboutDialog();
        await dialog.ShowDialog(this);
#else
        await System.Threading.Tasks.Task.CompletedTask;
#endif
    }
}
